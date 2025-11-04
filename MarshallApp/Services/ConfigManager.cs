using MarshallApp.Models;
using System.IO;
using System.Text.Json;

namespace MarshallApp
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = "blocks_config.json";

        public static void SaveAll(List<BlockConfig> configs)
        {
            var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        public static List<BlockConfig> LoadAll()
        {
            if (!File.Exists(ConfigPath))
                return new List<BlockConfig>();

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<List<BlockConfig>>(json) ?? new List<BlockConfig>();
        }
    }
}
