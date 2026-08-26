using System.IO.Compression;
using GitHubDesktopZh.Core.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace GitHubDesktopZh.Core.Services;

public class BackupManager
{
    private readonly string _backupRoot;

    public BackupManager(string backupRoot)
    {
        _backupRoot = backupRoot;
    }

    public async Task BackupFilesAsync(GitHubDesktopInfo desktopInfo, Manifest manifest)
    {
        var backupDir = GetBackupDirectory(desktopInfo.Version);
        if (Directory.Exists(backupDir))
        {
            var isChinese = IsBackupChinese(backupDir, manifest);
            if (isChinese)
            {
                Directory.Delete(backupDir, true);
            }
            else
            {
                return;
            }
        }

        // 尝试从旧版本复制英文原版
        var restoredFromOld = RestoreFromOldVersion(desktopInfo, manifest);
        if (restoredFromOld)
        {
            return;
        }

        Directory.CreateDirectory(backupDir);

        foreach (var file in manifest.Files)
        {
            var sourcePath = Path.Combine(desktopInfo.AppPath, file);
            var destPath = Path.Combine(backupDir, file);

            if (File.Exists(sourcePath))
            {
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(sourcePath, destPath, true);
            }
        }
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
                var fileName = Path.GetFileName(file);
                var entry = archive.Entries.FirstOrDefault(e =>
                    !e.IsDirectory &&
                    string.Equals(Path.GetFileName(e.Key), fileName, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    var destPath = Path.Combine(desktopInfo.AppPath, file);
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    using var entryStream = entry.OpenEntryStream();
                    using var destStream = File.Create(destPath);
                    await entryStream.CopyToAsync(destStream);
                }
            }
        }
        else
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "GitHubDesktopZh_Patch");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            try
            {
                ZipFile.ExtractToDirectory(patchFilePath, tempDir);

                foreach (var file in manifest.Files)
                {
                    var sourcePath = FindFileInDirectory(tempDir, file);
                    var destPath = Path.Combine(desktopInfo.AppPath, file);

                    if (sourcePath != null)
                    {
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);

                        File.Copy(sourcePath, destPath, true);
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

    public bool RestoreFiles(GitHubDesktopInfo desktopInfo)
    {
        var backupDir = GetBackupDirectory(desktopInfo.Version);
        if (!Directory.Exists(backupDir))
        {
            return false;
        }

        try
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("GitHub Desktop");
                foreach (var proc in processes)
                {
                    try { proc.Kill(); } catch { }
                }
                System.Threading.Thread.Sleep(1000);
            }
            catch { }

            CopyDirectory(backupDir, desktopInfo.AppPath);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestoreFiles failed: {ex.Message}");
            return false;
        }
    }

    public bool VerifyFiles(GitHubDesktopInfo desktopInfo, Manifest manifest)
    {
        foreach (var file in manifest.Files)
        {
            var filePath = Path.Combine(desktopInfo.AppPath, file);
            if (!File.Exists(filePath))
            {
                return false;
            }
        }

        return true;
    }

    public void CleanupOldBackups(int keepCount)
    {
        if (!Directory.Exists(_backupRoot))
            return;

        var directories = Directory.GetDirectories(_backupRoot)
            .OrderBy(d => Directory.GetLastWriteTime(d))
            .ToList();

        while (directories.Count > keepCount)
        {
            var dirToDelete = directories.First();
            Directory.Delete(dirToDelete, true);
            directories.RemoveAt(0);
        }
    }

    public void EnsureGitBinPath(GitHubDesktopInfo desktopInfo)
    {
        var appPath = desktopInfo.AppPath;
        var expectedGitPath = Path.Combine(appPath, "git", "bin", "git.exe");

        if (File.Exists(expectedGitPath))
            return;

        var candidates = new[]
        {
            Path.Combine(appPath, "git", "cmd", "git.exe"),
            Path.Combine(appPath, "git", "mingw64", "bin", "git.exe"),
            Path.Combine(appPath, "git", "usr", "bin", "git.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                var binDir = Path.GetDirectoryName(expectedGitPath);
                if (!string.IsNullOrEmpty(binDir) && !Directory.Exists(binDir))
                    Directory.CreateDirectory(binDir);

                File.Copy(candidate, expectedGitPath, true);
                return;
            }
        }
    }

    private bool IsBackupChinese(string backupDir, Manifest manifest)
    {
        foreach (var file in manifest.Files)
        {
            var filePath = Path.Combine(backupDir, Path.GetFileName(file));
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
                var filePath = Path.Combine(appPath, Path.GetFileName(file));
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
                    var sourcePath = Path.Combine(appPath, Path.GetFileName(file));
                    var destPath = Path.Combine(backupDir, Path.GetFileName(file));
                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, destPath, true);
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
        {
            Directory.CreateDirectory(destination);
        }

        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            try
            {
                File.Copy(file, destFile, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to copy {file}: {ex.Message}");
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
