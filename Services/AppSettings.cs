using System.IO;
using System.Text.Json;
using MultiSmbServer.Models;

namespace MultiSmbServer.Services;

public static class AppSettings
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiSmbServer");

    private static readonly string LegacySettingsFile =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PS2SmbServer2", "settings.json");

    private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static ServerConfig Load()
    {
        try
        {
            string? json = null;

            if (File.Exists(SettingsFile))
                json = File.ReadAllText(SettingsFile);
            else if (File.Exists(LegacySettingsFile))
                json = File.ReadAllText(LegacySettingsFile); // migración desde la carpeta de config anterior

            if (json != null)
            {
                ServerConfig config = JsonSerializer.Deserialize<ServerConfig>(json, JsonOptions) ?? new ServerConfig();
                config.Shares ??= new List<ShareConfig>();

                // Migración desde el formato anterior (un solo share con SharePath/ShareName).
                if (config.Shares.Count == 0)
                {
                    LegacyConfig? legacy = JsonSerializer.Deserialize<LegacyConfig>(json, JsonOptions);
                    if (legacy != null && !string.IsNullOrWhiteSpace(legacy.SharePath))
                    {
                        config.Shares.Add(new ShareConfig
                        {
                            Name = string.IsNullOrWhiteSpace(legacy.ShareName) ? "PS2SMB" : legacy.ShareName,
                            Path = legacy.SharePath
                        });
                    }
                }

                return config;
            }
        }
        catch
        {
            // Si falla la lectura, devolvemos los valores por defecto.
        }

        return new ServerConfig();
    }

    public static void Save(ServerConfig config)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Ignoramos errores de escritura: es solo una preferencia.
        }
    }

    private sealed class LegacyConfig
    {
        public string SharePath { get; set; } = string.Empty;

        public string ShareName { get; set; } = string.Empty;
    }
}
