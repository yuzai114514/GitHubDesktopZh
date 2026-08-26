using System.IO;
using GitHubDesktopZh.Core.Models;
using GitHubDesktopZh.Core.Services;

namespace GitHubDesktopZh.App.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsManager _settingsManager;
    private Settings _settings = new();

    private string _installationPath = string.Empty;
    private string _indexUrl = string.Empty;
    private int _checkIntervalMinutes = 60;
    private int _backupCount = 5;
    private bool _startWithWindows;
    private bool _silentStartup = true;

    public SettingsViewModel()
    {
        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktopZh");
        _settingsManager = new SettingsManager(Path.Combine(dataDirectory, "settings.json"));
    }

    public string InstallationPath
    {
        get => _installationPath;
        set => SetProperty(ref _installationPath, value);
    }

    public string IndexUrl
    {
        get => _indexUrl;
        set => SetProperty(ref _indexUrl, value);
    }

    public int CheckIntervalMinutes
    {
        get => _checkIntervalMinutes;
        set => SetProperty(ref _checkIntervalMinutes, value);
    }

    public int BackupCount
    {
        get => _backupCount;
        set => SetProperty(ref _backupCount, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool SilentStartup
    {
        get => _silentStartup;
        set => SetProperty(ref _silentStartup, value);
    }

    public async Task LoadSettingsAsync()
    {
        _settings = await _settingsManager.LoadSettingsAsync();
        InstallationPath = _settings.InstallationPath;
        IndexUrl = _settings.IndexUrl;
        CheckIntervalMinutes = _settings.CheckIntervalMinutes;
        BackupCount = _settings.BackupCount;
        StartWithWindows = _settings.StartWithWindows;
        SilentStartup = _settings.SilentStartup;
    }

    public async Task SaveSettingsAsync()
    {
        _settings.InstallationPath = InstallationPath;
        _settings.IndexUrl = IndexUrl;
        _settings.CheckIntervalMinutes = CheckIntervalMinutes;
        _settings.BackupCount = BackupCount;
        _settings.StartWithWindows = StartWithWindows;
        _settings.SilentStartup = SilentStartup;
        await _settingsManager.SaveSettingsAsync(_settings);
        UpdateStartupRegistry(StartWithWindows, SilentStartup);
    }

    private void UpdateStartupRegistry(bool enable, bool silent)
    {
        var appName = "GitHubDesktopZh";
        var exePath = Environment.ProcessPath ?? string.Empty;
        var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, true);
        if (key == null) return;

        if (enable)
        {
            var args = silent ? " --silent" : "";
            key.SetValue(appName, $"\"{exePath}\"{args}");
        }
        else
        {
            key.DeleteValue(appName, false);
        }
    }
}