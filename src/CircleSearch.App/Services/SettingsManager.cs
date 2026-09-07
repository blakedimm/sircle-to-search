using System;
using System.IO;
using System.Text.Json;
using CircleSearch.App.Config;

namespace CircleSearch.App.Services;

public sealed class SettingsManager
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CircleSearch");
    private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

    public AppSettings CurrentSettings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                return;
            }
        }
        catch { }

        CurrentSettings = new AppSettings();
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            string json = JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }
}