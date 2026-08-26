using System.Text.Json;
using GitHubDesktopZh.Core.Models;

namespace GitHubDesktopZh.Core.Services;

public class StateManager
{
    private readonly string _stateFilePath;

    public StateManager(string stateFilePath)
    {
        _stateFilePath = stateFilePath;
    }

    public async Task<State?> LoadStateAsync()
    {
        if (!File.Exists(_stateFilePath))
            return null;

        var json = await File.ReadAllTextAsync(_stateFilePath);
        return JsonSerializer.Deserialize<State>(json);
    }

    public async Task SaveStateAsync(State state)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_stateFilePath, json);
    }

    public async Task UpdateLocalizedVersionAsync(string version)
    {
        var state = await LoadStateAsync() ?? new State();
        state.LocalizedVersion = version;
        state.LastOperationTime = DateTime.Now;
        state.LastOperationResult = "success";
        await SaveStateAsync(state);
    }

    public async Task UpdateImportedFileHashesAsync(Dictionary<string, string> hashes)
    {
        var state = await LoadStateAsync() ?? new State();
        state.ImportedFileHashes = hashes;
        await SaveStateAsync(state);
    }

    public async Task UpdateLastCheckTimeAsync()
    {
        var state = await LoadStateAsync() ?? new State();
        state.LastCheckTime = DateTime.Now;
        await SaveStateAsync(state);
    }
}