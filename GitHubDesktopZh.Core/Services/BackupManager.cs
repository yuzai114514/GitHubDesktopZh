using System.IO.Compression;
using GitHubDesktopZh.Core.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace GitHubDesktopZh.Core.Services;

public class BackupManager
{
    private readonly string _backupRoot;
    private readonly DesktopProcessService _desktopProcess;
    private readonly Logger? _logger;

    public BackupManager(string backupRoot, Logger? logger = null)
    {
        _backupRoot = backupRoot;
        _desktopProcess = new DesktopProcessService();
        _logger = logger;
    }

    public async Task<bool> BackupFilesAsync(GitHubDesktopInfo desktopInfo, Manifest manifest)
    {
        var backupDir = GetBackupDirectory(desktopInfo.Version);
        if (Directory.Exists(backupDir))
        {
            var isChinese = IsBackupChinese(backupDir, manifest);
            if (isChinese)
            {
                Directory.Delete(backupDir, true);
                _logger?.Info("删除了包含中文内容的旧备份");
            }
            else
            {
                _logger?.Info("备份目录已存在且为英文原版，跳过备份");
                return true;
            }
        }

        var restoredFromOld = RestoreFromOldVersion(desktopInfo, manifest);
        if (restoredFromOld)
        {
            _logger?.Info("从旧版本恢复英文原版备份成功");
            return true;
        }

        Directory.CreateDirectory(backupDir);
        foreach (var file in manifest.Files)
        {
            SafePathResolver.EnsureSafePath(desktopInfo.AppPath, file, "备份");
            var sourcePath = SafePathResolver.ResolveSafePath(desktopInfo.AppPath, file);
            var destPath = SafePathResolver.ResolveSafePath(backupDir, file);

            if (File.Exists(sourcePath))
            {
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                File.Copy(sourcePath, destPath, true);
                _logger?.Debug($"备份文件: {file}");
            }
        }
        return true;
    }

    public async Task ImportFilesAsync(GitHubDesktopInfo desktopInfo, string patchFilePath, Manifest manifest)
    {
        var ext = Path.GetExtension(patchFilePath).ToLowerInvariant();
        bool isNonZip = ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz";

        if (isNonZip)
        {
            using var fileStream = File.OpenRead(patchFilePath);
            using var archive = ArchiveFactory.Open(fileStream);
            foreach (var file in manifest.Files)
            {
                SafePathResolver.EnsureSafePath(desktopInfo.AppPath, file, "导入");
                var fileName = Path.GetFileName(file);
                var entry = archive.Entries.FirstOrDefault(e =>
                    !e.IsDirectory &&
                    string.Equals(Path.GetFileName(e.Key), fileName, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    var destPath = SafePathResolver.ResolveSafePath(desktopInfo.AppPath, file);
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    using var entryStream = entry.OpenEntryStream();
                    using var destStream = File.Create(destPath);
                    await entryStream.CopyToAsync(destStream);
                    _logger?.Debug($"导入文件: {file}");
                }
                else
                {
                    _logger?.Warning($"补丁中未找到文件: {file}");
                }
            }
        }
        else
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"GitHubDesktopZh_Patch_{Guid.NewGuid():N}");
            try
            {
                ZipFile.ExtractToDirectory(patchFilePath, tempDir);

                foreach (var file in manifest.Files)
                {
                    SafePathResolver.EnsureSafePath(desktopInfo.AppPath, file, "导入");
                    var sourcePath = FindFileInDirectory(tempDir, file);
                    var destPath = SafePathResolver.ResolveSafePath(desktopInfo.AppPath, file);

                    if (sourcePath != null)
                    {
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);

                        File.Copy(sourcePath, destPath, true);
                        _logger?.Debug($"导入文件: {file}");
                    }
                    else
                    {
                        _logger?.Warning($"补丁中未找到文件: {file}");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }

    public async Task<bool> RestoreAsync(GitHubDesktopInfo desktopInfo)
    {
        var backupDir = GetBackupDirectory(desktopInfo.Version);
        if (!Directory.Exists(backupDir))
        {
            _logger?.Error("备份目录不存在，无法恢复");
            return false;
        }

        var closed = await _desktopProcess.CloseAndWaitAsync(5000);
        if (!closed)
        {
            _logger?.Error("无法关闭 GitHub Desktop，恢复中止");
            return false;
        }

        try
        {
            CopyDirectory(backupDir, desktopInfo.AppPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error($"恢复文件失败: {ex.Message}");
            return false;
        }
    }

    public bool VerifyFiles(GitHubDesktopInfo desktopInfo, Manifest manifest, Dictionary<string, string>? expectedHashes = null)
    {
        foreach (var file in manifest.Files)
        {
            SafePathResolver.EnsureSafePath(desktopInfo.AppPath, file, "验证");
            var filePath = SafePathResolver.ResolveSafePath(desktopInfo.AppPath, file);
            if (!File.Exists(filePath))
            {
                _logger?.Error($"验证失败: 文件不存在 {file}");
                return false;
            }

            if (expectedHashes != null && expectedHashes.TryGetValue(file, out var expectedHash))
            {
                var (success, actualHash, error) = FileIntegrityService.VerifyWithDetails(filePath, expectedHash);
                if (!success)
                {
                    _logger?.Error($"验证失败: {file} - {error}");
                    return false;
                }
                _logger?.Debug($"SHA-256 校验通过: {file} = {actualHash}");
            }
        }

        return true;
    }

    public void CleanupOldBackups(int keepCount)
    {
        if (!Directory.Exists(_backupRoot))
            return;

        var directories = Directory.GetDirectories(Path.Combine(_backupRoot, "backup"))
            .OrderBy(d => Directory.GetLastWriteTime(d))
            .ToList();

        while (directories.Count > keepCount)
        {
            var dirToDelete = directories.First();
            Directory.Delete(dirToDelete, true);
            _logger?.Info($"清理旧备份: {Path.GetFileName(dirToDelete)}");
            directories.RemoveAt(0);
        }
    }

    public void EnsureGitBinPath(GitHubDesktopInfo desktopInfo)
    {
        var appPath = desktopInfo.AppPath;
        var expectedGitPath = SafePathResolver.ResolveSafePath(appPath, "git/bin/git.exe");

        if (File.Exists(expectedGitPath))
            return;

        var candidates = new[]
        {
            "git/cmd/git.exe",
            "git/mingw64/bin/git.exe",
            "git/usr/bin/git.exe"
        };

        foreach (var candidate in candidates)
        {
            var candidatePath = SafePathResolver.ResolveSafePath(appPath, candidate);
            if (File.Exists(candidatePath))
            {
                var binDir = Path.GetDirectoryName(expectedGitPath);
                if (!string.IsNullOrEmpty(binDir) && !Directory.Exists(binDir))
                    Directory.CreateDirectory(binDir);

                File.Copy(candidatePath, expectedGitPath, true);
                _logger?.Info($"创建 git\\bin\\git.exe → {candidate}");
                return;
            }
        }
    }

    public async Task<bool> CloseDesktopAsync()
    {
        return await _desktopProcess.CloseAndWaitAsync(5000);
    }

    private bool IsBackupChinese(string backupDir, Manifest manifest)
    {
        foreach (var file in manifest.Files)
        {
            var filePath = SafePathResolver.ResolveSafePath(backupDir, file);
            if (!File.Exists(filePath)) continue;

            try
            {
                var content = File.ReadAllText(filePath);
                if (content.Contains("\u5b58\u50a8\u5e93") || content.Contains("\u5206\u652f") ||
                    content.Contains("\u63d0\u4ea4") || content.Contains("\u6c49\u5316") ||
                    content.Contains("\u4e2d\u6587"))
                {
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    private string GetBackupDirectory(string version)
    {
        return Path.Combine(_backupRoot, "backup", version);
    }

    private bool RestoreFromOldVersion(GitHubDesktopInfo desktopInfo, Manifest manifest)
    {
        var ghdBasePath = Path.GetDirectoryName(desktopInfo.AppPath);
        if (string.IsNullOrEmpty(ghdBasePath)) return false;

        var currentVersion = desktopInfo.Version;
        var oldVersions = Directory.GetDirectories(ghdBasePath, "app-*")
            .Where(d => !Path.GetFileName(d).Contains(currentVersion))
            .OrderByDescending(d => Directory.GetLastWriteTime(d))
            .ToList();

        foreach (var oldDir in oldVersions)
        {
            var appPath = Path.Combine(oldDir, "resources", "app");
            if (!Directory.Exists(appPath)) continue;

            var isChinese = false;
            foreach (var file in manifest.Files)
            {
                var filePath = SafePathResolver.ResolveSafePath(appPath, file);
                if (!File.Exists(filePath)) continue;
                try
                {
                    var content = File.ReadAllText(filePath);
                    if (content.Contains("\u5b58\u50a8\u5e93") || content.Contains("\u5206\u652f") ||
                        content.Contains("\u6c49\u5316") || content.Contains("\u4e2d\u6587"))
                    {
                        isChinese = true;
                        break;
                    }
                }
                catch { }
            }

            if (!isChinese)
            {
                var backupDir = GetBackupDirectory(desktopInfo.Version);
                Directory.CreateDirectory(backupDir);
                foreach (var file in manifest.Files)
                {
                    var sourcePath = SafePathResolver.ResolveSafePath(appPath, file);
                    var destPath = SafePathResolver.ResolveSafePath(backupDir, file);
                    if (File.Exists(sourcePath))
                    {
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        File.Copy(sourcePath, destPath, true);
                        _logger?.Info($"从旧版本 {Path.GetFileName(oldDir)} 复制原版文件: {file}");
                    }
                }
                return true;
            }
        }

        return false;
    }

    private void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(destination))
            Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            try
            {
                File.Copy(file, destFile, true);
            }
            catch (Exception ex)
            {
                _logger?.Error($"复制文件失败 {file}: {ex.Message}");
                throw;
            }
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(destination, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }

    private string? FindFileInDirectory(string rootDir, string fileName)
    {
        var directPath = Path.Combine(rootDir, fileName);
        if (File.Exists(directPath)) return directPath;

        foreach (var dir in Directory.GetDirectories(rootDir))
        {
            var found = FindFileInDirectory(dir, fileName);
            if (found != null) return found;
        }

        return null;
    }
}
