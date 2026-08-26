namespace GitHubDesktopZh.Core.Models;

public class GitHubDesktopInfo
{
    public string InstallationPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public bool IsUnpacked { get; set; }
    public bool HasAsar { get; set; }
    public string ResourcesPath { get; set; } = string.Empty;
    public string AppPath { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
}