namespace GitHubDesktopZh.Core.Models;

public class Manifest
{
    public string Version { get; set; } = string.Empty;
    public string[] Files { get; set; } = Array.Empty<string>();
    public string[] Allowlist { get; set; } = Array.Empty<string>();
    public Dictionary<string, string>? FileHashes { get; set; }
}