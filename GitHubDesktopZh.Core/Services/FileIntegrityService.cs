using System.Security.Cryptography;

namespace GitHubDesktopZh.Core.Services;

public static class FileIntegrityService
{
    public static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static string ComputeSha256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static bool VerifyFile(string filePath, string expectedHash)
    {
        if (!File.Exists(filePath))
            return false;

        if (string.IsNullOrWhiteSpace(expectedHash))
            return true;

        var actualHash = ComputeSha256(filePath);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    public static (bool success, string actualHash, string error) VerifyWithDetails(string filePath, string expectedHash)
    {
        if (!File.Exists(filePath))
            return (false, string.Empty, $"文件不存在: {filePath}");

        if (string.IsNullOrWhiteSpace(expectedHash))
            return (true, string.Empty, string.Empty);

        var actualHash = ComputeSha256(filePath);
        if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            return (true, actualHash, string.Empty);

        return (false, actualHash, $"SHA-256 不匹配: 预期 {expectedHash}, 实际 {actualHash}");
    }
}
