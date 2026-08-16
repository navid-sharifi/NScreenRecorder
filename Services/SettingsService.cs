using System;
using System.IO;
using System.Text.Json;
using ScreenRecorder.Models;

namespace ScreenRecorder.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        public SettingsModel CurrentSettings { get; private set; }

        public SettingsService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenRecorder");
            Directory.CreateDirectory(appDataPath);
            _settingsFilePath = Path.Combine(appDataPath, "settings.json");
            Load();
        }

        public void Load()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    CurrentSettings = JsonSerializer.Deserialize<SettingsModel>(json) ?? new SettingsModel();
                }
                catch
                {
                    CurrentSettings = new SettingsModel();
                }
            }
            else
            {
                CurrentSettings = new SettingsModel();
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}