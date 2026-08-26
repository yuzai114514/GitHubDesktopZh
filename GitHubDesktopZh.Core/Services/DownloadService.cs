using System.Security.Cryptography;
using GitHubDesktopZh.Core.Models;

namespace GitHubDesktopZh.Core.Services;

public class DownloadService
{
    private readonly HttpClient _httpClient;

    public DownloadService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<(bool Success, string FilePath, string Error)> DownloadPatchAsync(PatchEntry patch, string cacheDirectory)
    {
        try
        {
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            var uri = new Uri(patch.Url);
            var ext = Path.GetExtension(uri.LocalPath);
            if (string.IsNullOrEmpty(ext)) ext = ".zip";
            var fileName = $"GitHubDesktop-{patch.Version}-zh{ext}";
            var filePath = Path.Combine(cacheDirectory, fileName);

            byte[] content;
            if (patch.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                // Local patch (dev/test): read directly from disk.
                var localPath = new Uri(patch.Url).LocalPath;
                if (!File.Exists(localPath))
                {
                    return (false, string.Empty, $"本地补丁不存在: {localPath}");
                }
                content = await File.ReadAllBytesAsync(localPath);
            }
            else
            {
                using var response = await _httpClient.GetAsync(patch.Url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                content = ms.ToArray();
            }

            // Size check (体积核对)
            if (content.Length != patch.Size)
            {
                return (false, string.Empty, $"体积不符: 预期 {patch.Size} 字节, 实际 {content.Length} 字节");
            }

            await File.WriteAllBytesAsync(filePath, content);

            // SHA-256 check
            var hash = await ComputeSha256Async(filePath);
            if (!string.Equals(hash, patch.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(filePath);
                return (false, string.Empty, $"SHA-256 校验失败: 预期 {patch.Sha256}, 实际 {hash}");
            }

            return (true, filePath, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, ex.Message);
        }
    }

    public async Task<string> ComputeSha256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public bool VerifySha256(string filePath, string expectedHash)
    {
        var hash = ComputeSha256Async(filePath).GetAwaiter().GetResult();
        return string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
