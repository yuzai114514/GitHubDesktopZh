namespace GitHubDesktopZh.Core.Services;

public static class SafePathResolver
{
    public static string ResolveSafePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("路径不能为空", nameof(relativePath));

        var normalizedRoot = Path.GetFullPath(root);

        var combined = Path.Combine(normalizedRoot, relativePath);
        var fullPath = Path.GetFullPath(combined);

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"路径穿越被拒绝: '{relativePath}' 解析为 '{fullPath}'，超出根目录 '{normalizedRoot}'");

        return fullPath;
    }

    public static bool IsPathSafe(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        try
        {
            var normalizedRoot = Path.GetFullPath(root);
            var combined = Path.Combine(normalizedRoot, relativePath);
            var fullPath = Path.GetFullPath(combined);
            return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void EnsureSafePath(string root, string relativePath, string operation)
    {
        if (!IsPathSafe(root, relativePath))
        {
            var normalizedRoot = Path.GetFullPath(root);
            var combined = Path.Combine(normalizedRoot, relativePath);
            string fullPath;
            try { fullPath = Path.GetFullPath(combined); }
            catch { fullPath = combined; }
            throw new InvalidOperationException(
                $"[{operation}] 路径安全检查失败: '{relativePath}' → '{fullPath}' 不在 '{normalizedRoot}' 内");
        }
    }
}
