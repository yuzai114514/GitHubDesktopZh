using System.Net.Http.Json;
using System.Text.Json;
using GitHubDesktopZh.Core.Models;

namespace GitHubDesktopZh.Core.Services;

public class PatchIndexService
{
    private readonly HttpClient _httpClient;
    private readonly List<string> _indexUrls;
    private readonly string _localCachePath;
    private readonly string _bundledIndexPath;

    public PatchIndexService(string indexUrl, string localCachePath, string bundledIndexPath)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _indexUrls = new List<string> { indexUrl };
        _localCachePath = localCachePath;
        _bundledIndexPath = bundledIndexPath;
    }

    public PatchIndexService(List<string> indexUrls, string localCachePath, string bundledIndexPath)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _indexUrls = indexUrls.Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
        if (_indexUrls.Count == 0)
            _indexUrls.Add("https://raw.githubusercontent.com/743859910/GitHub_Desktop_Simplified_Chinese/master/resources/index.json");
        _localCachePath = localCachePath;
        _bundledIndexPath = bundledIndexPath;
    }

    public async Task<PatchIndex?> LoadIndexAsync()
    {
        // 1. Try remote URLs in order
        foreach (var url in _indexUrls)
        {
            try
            {
                var remoteIndex = await LoadRemoteIndexAsync(url);
                if (remoteIndex != null && remoteIndex.Patches.Length > 0)
                {
                    await SaveLocalCacheAsync(remoteIndex);
                    return remoteIndex;
                }
            }
            catch
            {
                // Try next URL
            }
        }

        // 2. Try local cache
        try
        {
            var cached = await LoadFromFileAsync(_localCachePath);
            if (cached != null && cached.Patches.Length > 0) return cached;
        }
        catch
        {
            // Ignore
        }

        // 3. Try bundled index (ships with the app)
        try
        {
            var bundled = await LoadFromFileAsync(_bundledIndexPath);
            if (bundled != null) return bundled;
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private async Task<PatchIndex?> LoadRemoteIndexAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PatchIndex>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private async Task<PatchIndex?> LoadFromFileAsync(string path)
    {
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<PatchIndex>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private async Task SaveLocalCacheAsync(PatchIndex index)
    {
        var directory = Path.GetDirectoryName(_localCachePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_localCachePath, json);
    }

    public PatchEntry? FindPatch(PatchIndex index, string desktopVersion)
    {
        foreach (var patch in index.Patches)
        {
            if (patch.Version == desktopVersion)
                return patch;

            if (patch.Compat != null && patch.Compat.Contains(desktopVersion))
                return patch;
        }

        return null;
    }
}