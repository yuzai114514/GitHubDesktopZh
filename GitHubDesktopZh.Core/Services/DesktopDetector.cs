using System.Diagnostics;
using System.Text.Json;
using GitHubDesktopZh.Core.Models;
using Microsoft.Win32;

namespace GitHubDesktopZh.Core.Services;

public class DesktopDetector
{
    private const string UninstallRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GitHubDesktop";
    private static readonly string DefaultInstallationPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktop");

    public GitHubDesktopInfo? Detect()
    {
        var installationPath = DetectInstallationPath();
        if (string.IsNullOrEmpty(installationPath) || !Directory.Exists(installationPath))
            return null;

        // Squirrel 布局：根目录下有多个 app-<version>，取版本最高的那个
        var appDir = FindNewestAppDirectory(installationPath);
        if (string.IsNullOrEmpty(appDir))
            return null;

        var version = ReadVersionFromPackageJson(appDir);
        if (string.IsNullOrEmpty(version))
            version = ReadVersionFromExe(appDir);
        if (string.IsNullOrEmpty(version))
            return null;

        var resourcesPath = Path.Combine(appDir, "resources");
        var appPath = Path.Combine(resourcesPath, "app");
        var exePath = Path.Combine(appDir, "GitHubDesktop.exe");
        var isUnpacked = File.Exists(Path.Combine(appPath, "main.js"));
        var hasAsar = File.Exists(Path.Combine(resourcesPath, "app.asar"));

        return new GitHubDesktopInfo
        {
            InstallationPath = installationPath,
            Version = version,
            Architecture = DetectArchitecture(exePath),
            IsUnpacked = isUnpacked,
            HasAsar = hasAsar,
            ResourcesPath = resourcesPath,
            AppPath = appPath,
            ExePath = exePath
        };
    }

    /// <summary>在根目录下找版本号最高的 app-* 目录（必须含 GitHubDesktop.exe）。</summary>
    public static string? FindNewestAppDirectory(string installationPath)
    {
        string? best = null;
        Version? bestVersion = null;
        foreach (var dir in Directory.GetDirectories(installationPath, "app-*"))
        {
            var versionText = Path.GetFileName(dir).Substring("app-".Length);
            if (!Version.TryParse(versionText, out var version))
                continue;
            if (!File.Exists(Path.Combine(dir, "GitHubDesktop.exe")))
                continue;
            if (bestVersion == null || version > bestVersion)
            {
                bestVersion = version;
                best = dir;
            }
        }
        return best;
    }

    private string DetectInstallationPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryKey);
        if (key != null)
        {
            var installLocation = key.GetValue("InstallLocation") as string;
            if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                return installLocation;
        }

        if (Directory.Exists(DefaultInstallationPath))
            return DefaultInstallationPath;

        return string.Empty;
    }

    private static string ReadVersionFromPackageJson(string appDir)
    {
        var packageJsonPath = Path.Combine(appDir, "resources", "app", "package.json");
        if (!File.Exists(packageJsonPath))
            return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
            if (doc.RootElement.TryGetProperty("version", out var versionElement))
                return versionElement.GetString() ?? string.Empty;
        }
        catch
        {
            // Ignore parsing errors
        }
        return string.Empty;
    }

    private static string ReadVersionFromExe(string appDir)
    {
        var exePath = Path.Combine(appDir, "GitHubDesktop.exe");
        if (!File.Exists(exePath))
            return string.Empty;
        try
        {
            return FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string DetectArchitecture(string exePath)
    {
        if (!File.Exists(exePath))
            return "unknown";

        try
        {
            using var stream = File.OpenRead(exePath);
            using var reader = new BinaryReader(stream);

            // DOS header -> PE offset -> COFF machine type
            stream.Seek(0x3C, SeekOrigin.Begin);
            var peOffset = reader.ReadInt32();
            stream.Seek(peOffset + 4, SeekOrigin.Begin);
            var machine = reader.ReadUInt16();

            return machine switch
            {
                0x8664 => "x64",
                0x014C => "x86",
                0xAA64 => "arm64",
                _ => "unknown"
            };
        }
        catch
        {
            return "unknown";
        }
    }

    public bool IsProcessRunning()
    {
        var processes = Process.GetProcessesByName("GitHubDesktop");
        return processes.Length > 0;
    }

    public void KillProcess()
    {
        var processes = Process.GetProcessesByName("GitHubDesktop");
        foreach (var process in processes)
        {
            try
            {
                process.Kill();
                process.WaitForExit(5000);
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
