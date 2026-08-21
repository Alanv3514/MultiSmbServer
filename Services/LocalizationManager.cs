using System.Collections.Generic;
using System.ComponentModel;

namespace MultiSmbServer.Services;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    public enum AppLanguage
    {
        English,
        Spanish
    }

    private static readonly LocalizationManager _instance = new();
    public static LocalizationManager Instance => _instance;

    private readonly Dictionary<string, string> _en = new();
    private readonly Dictionary<string, string> _es = new();

    private AppLanguage _language = AppLanguage.English;

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationManager()
    {
        RegisterStrings();
    }

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
                return;

            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        Dictionary<string, string> table = _language == AppLanguage.English ? _en : _es;
        return table.TryGetValue(key, out string? value) ? value : key;
    }

    private void Add(string key, string en, string es)
    {
        _en[key] = en;
        _es[key] = es;
    }

    private void RegisterStrings()
    {
        Add("Subtitle", "SMBv1 (NT LM 0.12) server for retro consoles: PS2, Wii and GameCube", "Servidor SMBv1 (NT LM 0.12) para consolas retro: PS2, Wii y GameCube");
        Add("SharesLabel", "Shares", "Shares");
        Add("ShareNameTooltip", "Share name", "Nombre del share");
        Add("SharePathTooltip", "Folder to share", "Carpeta a compartir");
        Add("BrowseButton", "Browse...", "Examinar...");
        Add("RemoveShareTooltip", "Remove share", "Quitar share");
        Add("AddShareButton", "+ Add share", "+ Agregar share");
        Add("PortLabel", "Listening port", "Puerto de escucha");
        Add("PortHint", "(445 = Direct TCP, 139 = NetBIOS, or a custom port)", "(445 = Direct TCP, 139 = NetBIOS, o un puerto personalizado)");
        Add("UserLabel", "User", "Usuario");
        Add("PasswordLabel", "Password", "Contraseña");
        Add("GuestCheckbox", "Allow Guest/Anonymous access (recommended: OPL usually connects without credentials)", "Permitir acceso Guest/Anónimo (recomendado: OPL suele conectar sin credenciales)");
        Add("LanOnlyCheckbox", "Only accept connections from the local network (LAN). Recommended to avoid exposing the server to the Internet.", "Solo aceptar conexiones de la red local (LAN). Recomendado para no exponer el servidor a Internet.");
        Add("StartButton", "Start server", "Iniciar servidor");
        Add("StopButton", "Stop server", "Detener servidor");
        Add("SaveConfigButton", "Save configuration", "Guardar configuración");
        Add("LogLabel", "Activity log", "Registro de actividad");
        Add("TrayOpen", "Open", "Abrir");
        Add("TrayExit", "Exit", "Salir");
        Add("TrayBalloon", "The application keeps running in the background. Double-click the tray icon to open it.", "La aplicación sigue en segundo plano. Doble clic en el icono de la bandeja para abrirla.");
        Add("ClosePrompt",
            "What do you want to do when closing the window?\n\n" +
            "·  Yes  →  Exit the application (stops the server).\n" +
            "·  No  →  Minimize to tray and keep serving in the background.\n" +
            "·  Cancel  →  Return to the application.",
            "¿Qué quieres hacer al cerrar la ventana?\n\n" +
            "·  Sí  →  Salir de la aplicación (detiene el servidor).\n" +
            "·  No  →  Minimizar a la bandeja y seguir sirviendo en segundo plano.\n" +
            "·  Cancelar  →  Volver a la aplicación.");
        Add("StatusStopped", "Stopped", "Detenido");
        Add("StatusRunning", "Server running on port {0} ({1} share(s))", "Servidor activo en el puerto {0} ({1} share(s))");
        Add("ConfigSaved", "Configuration saved.", "Configuración guardada.");
        Add("BrowseTitle", "Select the folder for share '{0}'", "Selecciona la carpeta del share '{0}'");
    }
}
