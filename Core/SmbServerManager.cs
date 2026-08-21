using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using MultiSmbServer.Models;
using SMBLibrary;
using SMBLibrary.Authentication.GSSAPI;
using SMBLibrary.Authentication.NTLM;
using SMBLibrary.Server;
using SMBLibrary.Win32;
using Utilities;

namespace MultiSmbServer.Core;

public sealed class SmbServerManager
{
    private SMBServer? _server;
    private ServerConfig? _config;

    private static readonly TimeSpan AccessLogThrottleInterval = TimeSpan.FromSeconds(1);
    private readonly Dictionary<string, DateTime> _accessLogThrottle = new();
    private readonly object _accessLogLock = new();

    public event Action<string>? OnLog;

    public bool IsRunning { get; private set; }

    public void Start(ServerConfig config)
    {
        if (IsRunning)
            throw new InvalidOperationException("El servidor ya está en ejecución.");

        List<ShareConfig> shares = config.Shares
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .ToList();

        if (shares.Count == 0)
            throw new ArgumentException("Agrega al menos un share con nombre y carpeta válidos.");

        foreach (ShareConfig shareConfig in shares)
        {
            if (string.IsNullOrWhiteSpace(shareConfig.Path) || !Directory.Exists(shareConfig.Path))
                throw new DirectoryNotFoundException($"La carpeta del share '{shareConfig.Name}' no existe: '{shareConfig.Path}'.");
        }

        if (config.Port < 1 || config.Port > 65535)
            throw new ArgumentException("El puerto debe estar entre 1 y 65535.");

        _config = config;

        var shareCollection = new SMBShareCollection();
        foreach (ShareConfig shareConfig in shares)
        {
            var fileStore = new NTDirectoryFileSystem(shareConfig.Path);
            var share = new FileSystemShare(shareConfig.Name, fileStore);
            share.AccessRequested += OnShareAccessRequested;
            shareCollection.Add(share);
        }

        var authenticationProvider = new IndependentNTLMAuthenticationProvider(GetUserPassword);
        var securityProvider = new GSSProvider(authenticationProvider);

        var server = new SMBServer(shareCollection, securityProvider);
        server.ConnectionRequested += OnConnectionRequested;
        server.LogEntryAdded += OnServerLogEntryAdded;

        SMBTransportType transport = config.Port == 139
            ? SMBTransportType.NetBiosOverTCP
            : SMBTransportType.DirectTCPTransport;

        try
        {
            // SMBLibrary fija el puerto según el transporte (445 DirectTCP / 139 NetBIOS).
            // Para soportar un puerto personalizado usamos la sobrecarga interna que acepta un puerto arbitrario.
            if (config.Port == 445 || config.Port == 139)
            {
                server.Start(IPAddress.Any, transport, enableSMB1: true, enableSMB2: false, enableSMB3: false);
            }
            else
            {
                StartServerOnPort(server, IPAddress.Any, transport, config.Port);
            }
        }
        catch (SocketException ex)
        {
            server.ConnectionRequested -= OnConnectionRequested;
            server.LogEntryAdded -= OnServerLogEntryAdded;
            _config = null;

            string hint = config.Port == 445
                ? "Si es el puerto 445, probablemente el servicio nativo de Windows (LanmanServer) lo está ocupando. " +
                  "Ejecuta en PowerShell como Administrador: Stop-Service LanmanServer -Force. "
                : string.Empty;

            throw new InvalidOperationException(
                $"No se pudo abrir el puerto {config.Port} ({ex.SocketErrorCode}). " + hint, ex);
        }

        _server = server;
        IsRunning = true;

        Log($"Servidor SMBv1 (NT LM 0.12) escuchando en el puerto {config.Port}.");
        foreach (ShareConfig shareConfig in shares)
            Log($"Share: \\\\{Environment.MachineName}\\{shareConfig.Name} -> {shareConfig.Path}");
        Log($"Autenticación: usuario '{config.Username}'" +
            (config.EnableGuest ? ", acceso Guest/Anónimo habilitado." : ", acceso Guest/Anónimo deshabilitado."));
    }

    public void Stop()
    {
        SMBServer? server = _server;
        if (server == null)
            return;

        server.Stop();
        server.ConnectionRequested -= OnConnectionRequested;
        server.LogEntryAdded -= OnServerLogEntryAdded;

        _server = null;
        _config = null;
        IsRunning = false;

        Log("Servidor detenido.");
    }

    private string? GetUserPassword(string userName)
    {
        ServerConfig? config = _config;
        if (config == null)
            return null;

        if (string.Equals(userName, "Guest", StringComparison.OrdinalIgnoreCase))
            return config.EnableGuest ? string.Empty : null;

        if (!string.IsNullOrEmpty(config.Username) &&
            string.Equals(userName, config.Username, StringComparison.OrdinalIgnoreCase))
        {
            return config.Password;
        }

        return null;
    }

    private void OnConnectionRequested(object? sender, ConnectionRequestEventArgs e)
    {
        if (_config is { LanOnly: true } && !IsLocalNetworkAddress(e.IPEndPoint.Address))
        {
            e.Accept = false;
            Log($"Rechazada conexión externa de {e.IPEndPoint.Address}:{e.IPEndPoint.Port} (solo se acepta LAN).");
            return;
        }

        e.Accept = true;
        Log($"Conexión entrante de {e.IPEndPoint.Address}:{e.IPEndPoint.Port}.");
    }

    private static bool IsLocalNetworkAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        byte[] bytes = address.GetAddressBytes();

        if (bytes.Length == 4)
        {
            // Rangos privados IPv4: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16.
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        // IPv6: link-local (fe80::/10) o Unique Local Address (fc00::/7).
        return address.IsIPv6LinkLocal || (bytes[0] & 0xFE) == 0xFC;
    }

    private void OnShareAccessRequested(object? sender, AccessRequestArgs e)
    {
        e.Allow = true;

        string access = e.RequestedAccess switch
        {
            FileAccess.Read => "lectura",
            FileAccess.Write => "escritura",
            _ => e.RequestedAccess.ToString()
        };

        // OPL abre/lee/cierra los archivos repetidamente durante la carga de un juego.
        // Limitamos el log de accesos para no saturar la UI: como mucho una línea por ruta cada segundo.
        string key = $"{access}|{e.UserName}|{e.Path}";
        lock (_accessLogLock)
        {
            if (_accessLogThrottle.TryGetValue(key, out DateTime last) &&
                DateTime.UtcNow - last < AccessLogThrottleInterval)
            {
                return;
            }

            if (_accessLogThrottle.Count > 1000)
                _accessLogThrottle.Clear();

            _accessLogThrottle[key] = DateTime.UtcNow;
        }

        Log($"Acceso de {access}: usuario '{e.UserName}' -> '{e.Path}' (desde {e.ClientEndPoint?.Address}).");
    }

    private void OnServerLogEntryAdded(object? sender, LogEntry e)
    {
        // Critical(1)..Warning(3) siempre; Information(4) solo si es un evento de conexión/autenticación.
        // Se omiten los eventos de archivo (Create/Close/FindFirst2/Read) para no inundar la UI durante el streaming.
        bool important = e.Severity <= Severity.Warning;
        bool connectionLevel = e.Severity == Severity.Information && IsConnectionLevelMessage(e.Message);

        if (important || connectionLevel)
            Log($"[SMB] {e.Message}");
    }

    private static readonly string[] ConnectionLevelInfoPrefixes =
    {
        "Session Setup:", "Tree Connect:", "Tree Disconnect:", "Logoff:", "New connection request"
    };

    private static bool IsConnectionLevelMessage(string message)
    {
        foreach (string prefix in ConnectionLevelInfoPrefixes)
        {
            if (message.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static readonly MethodInfo StartOnPortMethod = typeof(SMBServer).GetMethod(
        "Start",
        BindingFlags.NonPublic | BindingFlags.Instance,
        null,
        new[]
        {
            typeof(IPAddress),
            typeof(SMBTransportType),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(TimeSpan?)
        },
        null) ?? throw new MissingMethodException("SMBServer.Start (sobrecarga con puerto) no encontrada");

    private static void StartServerOnPort(SMBServer server, IPAddress address, SMBTransportType transport, int port)
    {
        StartOnPortMethod.Invoke(server, new object?[] { address, transport, port, true, false, false, null });
    }

    private void Log(string message) => OnLog?.Invoke(message);
}
