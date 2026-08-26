using System.Text.Json;
using GitHubDesktopZh.Core.Models;

namespace GitHubDesktopZh.Core.Services;

public class SettingsManager
{
    private readonly string _settingsFilePath;

    public SettingsManager(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    public async Task<Settings> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
            return new Settings();

        var json = await File.ReadAllTextAsync(_settingsFilePath);
        return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }
}