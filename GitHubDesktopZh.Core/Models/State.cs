namespace GitHubDesktopZh.Core.Models;

public class State
{
    public string? LocalizedVersion { get; set; }
    public Dictionary<string, string>? ImportedFileHashes { get; set; }
    public DateTime? LastCheckTime { get; set; }
    public DateTime? LastOperationTime { get; set; }
    public string? LastOperationResult { get; set; }
}