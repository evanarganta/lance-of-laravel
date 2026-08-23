using System;
using System.IO;
using System.Text.Json;
using LaravelLauncher.Models;

namespace LaravelLauncher.Services
{
    public class ProjectHistoryService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LaravelLauncher"
        );
        private static readonly string ConfigFilePath = Path.Combine(AppDataFolder, "config.json");

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public AppConfig LoadConfig()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                    if (config != null) return config;
                }
            }
            catch
            {
                // Fallback to default
            }

            return new AppConfig();
        }

        public void SaveConfig(AppConfig config)
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                string json = JsonSerializer.Serialize(config, JsonOpts);
                File.ReadAllText(ConfigFilePath); // check test or write
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to save config: " + ex.Message);
            }
        }
    }
}
