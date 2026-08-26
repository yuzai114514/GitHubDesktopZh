namespace GitHubDesktopZh.Core.Models;

public class PatchEntry
{
    public string Version { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Size { get; set; }
    public string[]? Compat { get; set; }
}