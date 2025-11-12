using MarshallApp.Models;
using System.IO;
using System.Text.Json;

namespace MarshallApp.Services;

public static class ConfigManager
{
    private static readonly string ConfigPath = "app_config.json";
    private static readonly JsonSerializerOptions options = new() { WriteIndented = true };

    public static void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(ConfigPath, json);
    }

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }
}
