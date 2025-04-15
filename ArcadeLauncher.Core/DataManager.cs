using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ArcadeLauncher.Core
{
    public static class DataManager
    {
        private static readonly string DataPath = Path.Combine(Program.InstallDir, "data.json");
        private static readonly string SettingsPath = Path.Combine(Program.InstallDir, "settings.json");

        public static GameData LoadGameData()
        {
            if (File.Exists(DataPath))
            {
                var json = File.ReadAllText(DataPath);
                return JsonSerializer.Deserialize<GameData>(json) ?? new GameData();
            }
            return new GameData();
        }

        public static void SaveGameData(GameData data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DataPath, json);
        }

        public static Settings LoadSettings()
        {
            Settings settings;
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            else
            {
                settings = new Settings();
            }

            // Ensure InputMappings has all required keys
            if (settings.InputMappings == null)
            {
                settings.InputMappings = new Dictionary<string, List<string>>();
            }

            // Migration: Rename "Launch" to "Select" if "Launch" exists
            if (settings.InputMappings.ContainsKey("Launch"))
            {
                if (!settings.InputMappings.ContainsKey("Select"))
                {
                    // Copy the "Launch" key bindings to "Select"
                    settings.InputMappings["Select"] = new List<string>(settings.InputMappings["Launch"]);
                }
                // Remove the "Launch" key
                settings.InputMappings.Remove("Launch");
            }

            // Ensure all required keys exist
            string[] requiredKeys = new[] { "Left", "Right", "Up", "Down", "Select", "Exit" };
            foreach (var key in requiredKeys)
            {
                if (!settings.InputMappings.ContainsKey(key))
                {
                    settings.InputMappings[key] = new List<string>();
                }
            }

            return settings;
        }

        public static void SaveSettings(Settings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }

        public static string GetGameAssetPath(string gameName, string assetType)
        {
            var gameDir = Path.Combine(Program.InstallDir, "Assets", gameName);
            Directory.CreateDirectory(gameDir);
            return Path.Combine(gameDir, $"{assetType}.png");
        }
    }
}