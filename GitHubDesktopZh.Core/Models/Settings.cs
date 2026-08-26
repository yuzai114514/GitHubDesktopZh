namespace GitHubDesktopZh.Core.Models;

public class Settings
{
    public string InstallationPath { get; set; } = string.Empty;
    public string IndexUrl { get; set; } = @"https://raw.githubusercontent.com/743859910/GitHub_Desktop_Simplified_Chinese/master/resources/index.json
https://raw.githubusercontent.com/cngege/GitHubDesktop2Chinese/main/json/localization.json
https://raw.githubusercontent.com/zetaloop/desktop/l10n/resources/index.json
https://raw.githubusercontent.com/lkyero/GitHubDesktop_zh/master/resources/index.json
https://raw.githubusercontent.com/goldsv2026/GitHub_Desktop_Simplified_Chinese/master/resources/index.json";
    public bool AutoCheck { get; set; } = true;
    public bool AutoLocalize { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool SilentStartup { get; set; } = true;
    public int CheckIntervalMinutes { get; set; } = 60;
    public int BackupCount { get; set; } = 5;
}