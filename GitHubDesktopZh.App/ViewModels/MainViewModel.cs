using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using GitHubDesktopZh.Core.Models;
using GitHubDesktopZh.Core.Services;
using SharpCompress.Archives;

namespace GitHubDesktopZh.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly DesktopDetector _desktopDetector;
    private PatchIndexService _patchIndexService;
    private readonly DownloadService _downloadService;
    private readonly BackupManager _backupManager;
    private readonly StateManager _stateManager;
    private readonly SettingsManager _settingsManager;
    private readonly Logger _logger;

    private GitHubDesktopInfo? _desktopInfo;
    private PatchIndex? _patchIndex;
    private State? _state;
    private Settings _settings = new();

    private string _statusMessage = string.Empty;
    private string _desktopVersion = string.Empty;
    private string _localizedVersion = string.Empty;
    private string _lastCheckTime = string.Empty;
    private string _lastOperationTime = string.Empty;
    private bool _autoCheck = true;
    private bool _autoLocalize = false;
    private bool _startWithWindows = false;
    private bool _silentStartup = true;
    private bool _isBusy = false;
    private string _indexUrl = string.Empty;
    private List<string> _indexUrlList = new();
    private int _checkIntervalMinutes = 60;
    private int _backupCount = 5;
    private string _statusColor = "Gray";
    private string _architectureText = string.Empty;
    private string _resourceLayoutText = string.Empty;
    private string _installationPath = string.Empty;
    private string _availablePatchInfo = string.Empty;
    private string _currentStep = string.Empty;
    private double _progressValue = 0;
    private string _progressText = string.Empty;
    private readonly System.Text.StringBuilder _logBuffer = new();

    private readonly SemaphoreSlim _operationLock = new(1, 1);

    private const string DefaultIndexUrl = @"https://raw.githubusercontent.com/743859910/GitHub_Desktop_Simplified_Chinese/master/resources/index.json
https://raw.githubusercontent.com/cngege/GitHubDesktop2Chinese/main/json/localization.json
https://raw.githubusercontent.com/zetaloop/desktop/l10n/resources/index.json
https://raw.githubusercontent.com/lkyero/GitHubDesktop_zh/master/resources/index.json
https://raw.githubusercontent.com/goldsv2026/GitHub_Desktop_Simplified_Chinese/master/resources/index.json";
    private readonly string _dataDirectory;

    public MainViewModel()
    {
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktopZh");
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _desktopDetector = new DesktopDetector();
        _patchIndexService = new PatchIndexService(
            DefaultIndexUrl,
            Path.Combine(_dataDirectory, "cache", "index.json"),
            Path.Combine(appDirectory, "resources", "index.json"));
        _downloadService = new DownloadService();
        _backupManager = new BackupManager(_dataDirectory, _logger);
        _stateManager = new StateManager(Path.Combine(_dataDirectory, "state.json"));
        _settingsManager = new SettingsManager(Path.Combine(_dataDirectory, "settings.json"));
        _logger = new Logger(Path.Combine(_dataDirectory, "logs"));
        _backupManager = new BackupManager(_dataDirectory, _logger);

        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
        LocalizeCommand = new AsyncRelayCommand(LocalizeAsync);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync);
        DownloadLatestCommand = new AsyncRelayCommand(DownloadLatestPatchAsync);
        CheckAppUpdatesCommand = new AsyncRelayCommand(CheckAppUpdatesAsync);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string DesktopVersion
    {
        get => _desktopVersion;
        set => SetProperty(ref _desktopVersion, value);
    }

    public string LocalizedVersion
    {
        get => _localizedVersion;
        set => SetProperty(ref _localizedVersion, value);
    }

    public string LastCheckTime
    {
        get => _lastCheckTime;
        set => SetProperty(ref _lastCheckTime, value);
    }

    public string LastOperationTime
    {
        get => _lastOperationTime;
        set => SetProperty(ref _lastOperationTime, value);
    }

    public bool AutoCheck
    {
        get => _autoCheck;
        set
        {
            if (SetProperty(ref _autoCheck, value))
            {
                _settings.AutoCheck = value;
                _ = _settingsManager.SaveSettingsAsync(_settings);
            }
        }
    }

    public bool AutoLocalize
    {
        get => _autoLocalize;
        set
        {
            if (SetProperty(ref _autoLocalize, value))
            {
                _settings.AutoLocalize = value;
                _ = _settingsManager.SaveSettingsAsync(_settings);
            }
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                _settings.StartWithWindows = value;
                _ = _settingsManager.SaveSettingsAsync(_settings);
                UpdateStartupRegistry(value, _silentStartup);
            }
        }
    }

    public bool SilentStartup
    {
        get => _silentStartup;
        set
        {
            if (SetProperty(ref _silentStartup, value))
            {
                _settings.SilentStartup = value;
                _ = _settingsManager.SaveSettingsAsync(_settings);
                UpdateStartupRegistry(_startWithWindows, value);
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public string ArchitectureText
    {
        get => _architectureText;
        set => SetProperty(ref _architectureText, value);
    }

    public string ResourceLayoutText
    {
        get => _resourceLayoutText;
        set => SetProperty(ref _resourceLayoutText, value);
    }

    public string InstallationPath
    {
        get => _installationPath;
        set => SetProperty(ref _installationPath, value);
    }

    public string AvailablePatchInfo
    {
        get => _availablePatchInfo;
        set => SetProperty(ref _availablePatchInfo, value);
    }

    public string CurrentStep
    {
        get => _currentStep;
        set => SetProperty(ref _currentStep, value);
    }

    public string LogText
    {
        get => _logBuffer.ToString();
    }

    public string IndexUrl
    {
        get => _indexUrl;
        set
        {
            if (SetProperty(ref _indexUrl, value))
            {
                _settings.IndexUrl = value;
                _ = _settingsManager.SaveSettingsAsync(_settings);
                IndexUrlList = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }
    }

    public List<string> IndexUrlList
    {
        get => _indexUrlList;
        set => SetProperty(ref _indexUrlList, value);
    }

    public int CheckIntervalMinutes
    {
        get => _checkIntervalMinutes;
        set
        {
            if (SetProperty(ref _checkIntervalMinutes, value))
            {
                _settings.CheckIntervalMinutes = value;
                _ = _settingsManager.SaveSettingsAsync(_settings);
            }
        }
    }

    public int BackupCount
    {
        get => _backupCount;
        set
        {
            if (SetProperty(ref _backupCount, value))
            {
                _settings.BackupCount = value;
                _ = _settingsManager.SaveSettingsAsync(_settings);
            }
        }
    }

    private void AppendLog(string message, string level = "INFO")
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logBuffer.AppendLine($"[{timestamp}] [{level}] {message}");
        OnPropertyChanged(nameof(LogText));
    }

    public GitHubDesktopInfo? DesktopInfo => _desktopInfo;

    public AsyncRelayCommand CheckForUpdatesCommand { get; } = null!;
    public AsyncRelayCommand LocalizeCommand { get; } = null!;
    public AsyncRelayCommand RestoreCommand { get; } = null!;
    public AsyncRelayCommand DownloadLatestCommand { get; } = null!;
    public AsyncRelayCommand CheckAppUpdatesCommand { get; } = null!;

    public async Task InitializeAsync()
    {
        _settings = await _settingsManager.LoadSettingsAsync();
        AutoCheck = _settings.AutoCheck;
        AutoLocalize = _settings.AutoLocalize;
        StartWithWindows = _settings.StartWithWindows;
        SilentStartup = _settings.SilentStartup;
        _indexUrl = _settings.IndexUrl;
        IndexUrlList = _indexUrl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        _checkIntervalMinutes = _settings.CheckIntervalMinutes;
        _backupCount = _settings.BackupCount;
        OnPropertyChanged(nameof(IndexUrl));
        OnPropertyChanged(nameof(IndexUrlList));
        OnPropertyChanged(nameof(CheckIntervalMinutes));
        OnPropertyChanged(nameof(BackupCount));

        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var urls = _settings.IndexUrl
            .Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();
        if (urls.Count == 0)
        {
            urls.AddRange(DefaultIndexUrl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrWhiteSpace(u)));
        }
        _patchIndexService = new PatchIndexService(
            urls,
            Path.Combine(_dataDirectory, "cache", "index.json"),
            Path.Combine(appDirectory, "resources", "index.json"));

        _state = await _stateManager.LoadStateAsync();
        DetectDesktop();
    }

    public void DetectDesktop()
    {
        _desktopInfo = _desktopDetector.Detect();
        if (_desktopInfo != null)
        {
            DesktopVersion = _desktopInfo.Version;
            ArchitectureText = _desktopInfo.Architecture;
            ResourceLayoutText = _desktopInfo.IsUnpacked ? "解包模式" : _desktopInfo.HasAsar ? "ASAR 模式" : "未知";
            InstallationPath = _desktopInfo.InstallationPath;
            StatusMessage = "已检测到 GitHub Desktop";
            StatusColor = "Green";
            _logger.Info($"Detected GitHub Desktop {_desktopInfo.Version} at {_desktopInfo.InstallationPath}");
        }
        else
        {
            StatusMessage = "未检测到 GitHub Desktop";
            DesktopVersion = string.Empty;
            ArchitectureText = string.Empty;
            ResourceLayoutText = string.Empty;
            InstallationPath = string.Empty;
            StatusColor = "Red";
            _logger.Warning("GitHub Desktop not detected");
        }

        if (_state?.LocalizedVersion != null)
            LocalizedVersion = _state.LocalizedVersion;
        else
            LocalizedVersion = string.Empty;

        if (_state?.LastCheckTime != null)
            LastCheckTime = _state.LastCheckTime.Value.ToString("yyyy-MM-dd HH:mm:ss");

        if (_state?.LastOperationTime != null)
            LastOperationTime = _state.LastOperationTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public async Task CheckForUpdatesAsync()
    {
        if (_desktopInfo == null)
        {
            StatusMessage = "请先检测 GitHub Desktop";
            return;
        }

        if (!await _operationLock.WaitAsync(0))
        {
            StatusMessage = "已有任务正在执行，请稍候";
            AppendLog("检查更新跳过：已有任务在执行", "WARN");
            return;
        }

        try
        {
            IsBusy = true;
            CurrentStep = "正在连接资源仓库...";
            StatusMessage = "正在检查更新...";
            _logger.Info("Checking for updates");
            AppendLog("开始检查更新");

            _patchIndex = await _patchIndexService.LoadIndexAsync();
            if (_patchIndex == null || _patchIndex.Patches.Length == 0)
            {
                StatusMessage = "未获取到任何补丁资源";
                AvailablePatchInfo = "请检查资源仓库地址是否正确";
                _logger.Error("Failed to load patch index or index is empty");
                AppendLog("加载补丁索引失败或索引为空", "ERROR");
                return;
            }

            CurrentStep = "正在匹配版本...";

            var allVersions = string.Join(", ", _patchIndex.Patches.Select(p => p.Version));
            _logger.Info($"Available patches: {allVersions}");
            AppendLog($"索引加载成功，可用版本: {allVersions}");

            var patch = _patchIndexService.FindPatch(_patchIndex, _desktopInfo.Version);
            if (patch != null)
            {
                StatusMessage = $"找到精确匹配补丁版本 {patch.Version}";
                AvailablePatchInfo = $"精确匹配: {patch.Version}  |  大小 {FormatSize(patch.Size)}  |  SHA-256 已校验";
                _logger.Info($"Found exact patch {patch.Version}");
                AppendLog($"精确匹配到补丁版本 {patch.Version}");
            }
            else
            {
                PatchEntry? fallback = null;
                if (Version.TryParse(_desktopInfo.Version, out var desktopVer))
                {
                    int bestDist = int.MaxValue;
                    foreach (var p in _patchIndex.Patches)
                    {
                        if (!Version.TryParse(p.Version, out var pv)) continue;
                        int dist = Math.Abs(desktopVer.Major - pv.Major) * 10000
                                  + Math.Abs(desktopVer.Minor - pv.Minor) * 100
                                  + Math.Abs(desktopVer.Build - pv.Build);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            fallback = p;
                        }
                    }
                }
                else if (_patchIndex.Patches.Length > 0)
                {
                    fallback = _patchIndex.Patches[0];
                }

                if (fallback != null)
                {
                    StatusMessage = $"当前版本 {_desktopInfo.Version} 无精确匹配补丁";
                    AvailablePatchInfo = $"最接近补丁: {fallback.Version}（向下兼容）  |  大小 {FormatSize(fallback.Size)}  |  可用版本: {allVersions}";
                    _logger.Info($"No exact patch for {_desktopInfo.Version}, closest: {fallback.Version}");
                    AppendLog($"当前版本 {_desktopInfo.Version} 无精确匹配，最接近版本: {fallback.Version}");
                }
                else
                {
                    StatusMessage = $"未找到适用于版本 {_desktopInfo.Version} 的补丁";
                    AvailablePatchInfo = $"资源仓库共有 {_patchIndex.Patches.Length} 个补丁，版本: {allVersions}";
                    _logger.Info($"No compatible patch found for version {_desktopInfo.Version}");
                    AppendLog($"未找到兼容的补丁版本", "WARN");
                }
            }

            await _stateManager.UpdateLastCheckTimeAsync();
            LastCheckTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (Exception ex)
        {
            StatusMessage = $"检查更新失败: {ex.Message}";
            AvailablePatchInfo = string.Empty;
            _logger.Error($"Check for updates failed: {ex.Message}");
            AppendLog($"检查更新失败: {ex.Message}", "ERROR");
        }
        finally
        {
            CurrentStep = string.Empty;
            IsBusy = false;
            _operationLock.Release();
        }
    }

    public async Task LocalizeAsync()
    {
        if (_desktopInfo == null)
        {
            StatusMessage = "请先检测 GitHub Desktop";
            AppendLog("未检测到 GitHub Desktop", "ERROR");
            return;
        }

        if (!await _operationLock.WaitAsync(0))
        {
            StatusMessage = "已有任务正在执行，请稍候";
            AppendLog("汉化跳过：已有任务在执行", "WARN");
            return;
        }

        try
        {
            if (_patchIndex == null)
            {
                _patchIndex = await _patchIndexService.LoadIndexAsync();
                if (_patchIndex == null)
                {
                    StatusMessage = "无法加载补丁索引";
                    AppendLog("无法加载补丁索引", "ERROR");
                    return;
                }
            }

            var patch = _patchIndexService.FindPatch(_patchIndex, _desktopInfo.Version);
            if (patch == null)
            {
                StatusMessage = $"未找到适用于版本 {_desktopInfo.Version} 的精确匹配补丁，正在查找可用补丁...";
                _logger.Info($"No exact patch for version {_desktopInfo.Version}, searching for compatible patch");
                AppendLog($"当前版本 {_desktopInfo.Version} 无精确匹配，查找最接近版本...");

                PatchEntry? fallback = null;
                if (Version.TryParse(_desktopInfo.Version, out var desktopVer))
                {
                    int bestDist = int.MaxValue;
                    foreach (var p in _patchIndex.Patches)
                    {
                        if (!Version.TryParse(p.Version, out var pv)) continue;
                        int dist = Math.Abs(desktopVer.Major - pv.Major) * 10000
                                  + Math.Abs(desktopVer.Minor - pv.Minor) * 100
                                  + Math.Abs(desktopVer.Build - pv.Build);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            fallback = p;
                        }
                    }
                }
                else if (_patchIndex.Patches.Length > 0)
                {
                    fallback = _patchIndex.Patches[0];
                }

                if (fallback == null)
                {
                    StatusMessage = "索引中没有可用补丁";
                    AppendLog("索引中没有可用补丁", "ERROR");
                    return;
                }

                patch = fallback;

                var isExactMatch = _patchIndexService.FindPatch(_patchIndex, _desktopInfo.Version) != null;
                if (!isExactMatch)
                {
                    var confirmResult = System.Windows.MessageBox.Show(
                        $"当前 GitHub Desktop 版本：{_desktopInfo.Version}\n" +
                        $"准备使用的补丁版本：{patch.Version}\n\n" +
                        $"该补丁没有明确声明兼容当前版本。\n" +
                        $"继续使用可能导致 GitHub Desktop 部分功能异常或无法启动。\n\n" +
                        $"是否继续？",
                        "版本兼容性确认",
                        System.Windows.MessageBoxButton.OKCancel,
                        System.Windows.MessageBoxImage.Warning);

                    if (confirmResult != System.Windows.MessageBoxResult.OK)
                    {
                        StatusMessage = "用户取消了汉化操作";
                        AppendLog("用户取消了版本不匹配的汉化操作");
                        return;
                    }
                }

                StatusMessage = $"将尝试安装补丁版本 {patch.Version}（向下兼容）";
                AppendLog($"选择兼容补丁版本 {patch.Version}");
            }

            IsBusy = true;
            string? backupDirBefore = Path.Combine(_dataDirectory, "backup", _desktopInfo.Version);
            bool backupExistedBefore = Directory.Exists(backupDirBefore);

            try
            {
                // Step 1: Download
                CurrentStep = "① 下载汉化资源...";
                StatusMessage = "正在下载补丁...";
                _logger.Info($"Downloading patch {patch.Version}");
                AppendLog($"开始下载补丁 {patch.Version}，URL: {patch.Url}");

                var cacheDirectory = Path.Combine(_dataDirectory, "cache");
                var result = await _downloadService.DownloadPatchAsync(patch, cacheDirectory);

                if (!result.Success)
                {
                    StatusMessage = $"汉化资源校验失败: {result.Error}。本次操作已取消";
                    _logger.Error($"Download failed: {result.Error}");
                    AppendLog($"下载失败: {result.Error}", "ERROR");
                    return;
                }
                AppendLog($"下载成功，文件: {result.FilePath}");

                // Step 2: Load manifest
                CurrentStep = "② 解析补丁清单...";
                StatusMessage = "正在解析补丁...";
                var manifest = LoadManifestFromZip(result.FilePath);
                if (manifest == null)
                {
                    AppendLog("未找到 manifest.json，尝试识别压缩包中的补丁文件");
                    manifest = CreateDefaultManifest(result.FilePath, patch.Version);
                    if (manifest == null)
                    {
                        StatusMessage = "无法加载补丁清单，压缩包中未找到可识别的补丁文件";
                        _logger.Error("Failed to load manifest and no patch files found");
                        AppendLog("压缩包中未找到 main.js/renderer.js 等补丁文件", "ERROR");
                        return;
                    }
                    AppendLog($"从压缩包中识别到 {manifest.Files.Length} 个补丁文件: {string.Join(", ", manifest.Files)}");
                }
                else
                {
                    AppendLog($"清单版本: {manifest.Version}，文件数: {manifest.Files.Length}");
                }

                if (!string.Equals(manifest.Version, _desktopInfo.Version, StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = $"警告: 补丁版本 ({manifest.Version}) 与 Desktop 版本 ({_desktopInfo.Version}) 不一致，尝试兼容安装...";
                    _logger.Warning($"Manifest version {manifest.Version} != desktop version {_desktopInfo.Version}, attempting compatible install");
                    AppendLog($"版本不一致: 补丁 {manifest.Version} ≠ Desktop {_desktopInfo.Version}，尝试兼容安装", "WARN");
                }

                // Validate all paths before any file operations
                CurrentStep = "②⑤ 校验补丁路径...";
                foreach (var file in manifest.Files)
                {
                    SafePathResolver.EnsureSafePath(_desktopInfo.AppPath, file, "汉化路径预检");
                }
                AppendLog("所有补丁路径安全检查通过");

                // Step 3: Close GitHub Desktop
                CurrentStep = "③ 关闭 GitHub Desktop...";
                StatusMessage = "正在关闭 GitHub Desktop...";
                AppendLog("正在关闭 GitHub Desktop");
                var closed = await _backupManager.CloseDesktopAsync();
                if (!closed)
                {
                    StatusMessage = "无法关闭 GitHub Desktop，请手动关闭后重试";
                    AppendLog("GitHub Desktop 无法关闭，汉化中止", "ERROR");
                    return;
                }
                AppendLog("GitHub Desktop 已关闭");

                // Step 4: Backup
                CurrentStep = "④ 备份原始文件...";
                StatusMessage = "正在备份文件...";
                _logger.Info("Backing up files");
                AppendLog("开始备份原始文件");
                var backupOk = await _backupManager.BackupFilesAsync(_desktopInfo, manifest);
                if (!backupOk)
                {
                    StatusMessage = "备份失败，汉化中止";
                    AppendLog("备份失败，汉化中止", "ERROR");
                    return;
                }
                AppendLog("备份完成");

                // Step 5: Import
                CurrentStep = "⑤ 导入汉化文件...";
                StatusMessage = "正在导入文件...";
                _logger.Info("Importing files");
                AppendLog("开始导入汉化文件");
                await _backupManager.ImportFilesAsync(_desktopInfo, result.FilePath, manifest);
                AppendLog("汉化文件导入完成");

                // Step 5.5: Ensure git\bin\git.exe exists
                CurrentStep = "⑤⑤ 检查 git 路径...";
                _backupManager.EnsureGitBinPath(_desktopInfo);
                AppendLog("git 路径检查完成");

                // Step 6: Verify file integrity
                CurrentStep = "⑥ 验证文件完整性...";
                StatusMessage = "正在验证...";
                Dictionary<string, string>? expectedHashes = null;
                if (manifest.FileHashes != null && manifest.FileHashes.Count > 0)
                {
                    expectedHashes = manifest.FileHashes;
                }
                if (!_backupManager.VerifyFiles(_desktopInfo, manifest, expectedHashes))
                {
                    StatusMessage = "文件验证失败，正在恢复...";
                    _logger.Error("File verification failed, restoring");
                    AppendLog("文件验证失败，正在回滚", "ERROR");
                    var restoreOk = await _backupManager.RestoreAsync(_desktopInfo);
                    if (restoreOk)
                    {
                        StatusMessage = "汉化失败，GitHub Desktop 已恢复到修改前状态";
                        AppendLog("回滚成功，GitHub Desktop 已恢复");
                    }
                    else
                    {
                        StatusMessage = "汉化失败且自动恢复失败，请手动恢复备份";
                        AppendLog("回滚失败，请手动恢复备份", "ERROR");
                    }
                    return;
                }
                AppendLog("文件验证通过（SHA-256 + 文件存在性）");

                // Step 7: Done
                CurrentStep = "⑦ 汉化完成！";
                StatusMessage = "汉化完成";
                StatusColor = "Green";
                _logger.Info("Localization completed");
                AppendLog("汉化完成！");

                await _stateManager.UpdateLocalizedVersionAsync(_desktopInfo.Version);
                LocalizedVersion = _desktopInfo.Version;

                await _stateManager.UpdateLastCheckTimeAsync();
                LastOperationTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                StatusMessage = $"汉化失败: {ex.Message}";
                _logger.Error($"Localization failed: {ex.Message}");
                AppendLog($"汉化失败: {ex.Message}", "ERROR");

                try
                {
                    AppendLog("正在尝试自动恢复...");
                    var restoreOk = await _backupManager.RestoreAsync(_desktopInfo);
                    if (restoreOk)
                    {
                        StatusMessage = "汉化失败，GitHub Desktop 已自动恢复";
                        AppendLog("自动恢复成功");
                    }
                    else
                    {
                        StatusMessage = "汉化失败且自动恢复失败，请手动恢复备份";
                        AppendLog("自动恢复失败，请手动恢复备份", "ERROR");
                    }
                }
                catch (Exception restoreEx)
                {
                    StatusMessage = "汉化失败且自动恢复异常，请手动恢复备份";
                    AppendLog($"自动恢复异常: {restoreEx.Message}", "ERROR");
                }
            }
            finally
            {
                IsBusy = false;
                CurrentStep = string.Empty;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task RestoreAsync()
    {
        if (_desktopInfo == null)
        {
            StatusMessage = "请先检测 GitHub Desktop";
            AppendLog("未检测到 GitHub Desktop", "ERROR");
            return;
        }

        if (!await _operationLock.WaitAsync(0))
        {
            StatusMessage = "已有任务正在执行，请稍候";
            AppendLog("恢复跳过：已有任务在执行", "WARN");
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在恢复...";
            CurrentStep = "正在恢复原始文件...";
            _logger.Info("Restoring files");
            AppendLog("开始恢复原始文件");

            var success = await _backupManager.RestoreAsync(_desktopInfo);
            if (success)
            {
                StatusMessage = "恢复完成";
                StatusColor = "Gray";
                _logger.Info("Restore completed");
                AppendLog("恢复完成");
                await _stateManager.UpdateLocalizedVersionAsync(string.Empty);
                LocalizedVersion = string.Empty;
            }
            else
            {
                StatusMessage = "恢复失败，可能是因为 GitHub Desktop 正在运行，请先关闭 GitHub Desktop 再试";
                _logger.Error("Restore failed");
                AppendLog("恢复失败，可能文件被占用，请关闭 GitHub Desktop 后重试", "ERROR");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"恢复失败: {ex.Message}";
            _logger.Error($"Restore failed: {ex.Message}");
            AppendLog($"恢复失败: {ex.Message}", "ERROR");
        }
        finally
        {
            IsBusy = false;
            CurrentStep = string.Empty;
            _operationLock.Release();
        }
    }

    public async Task DownloadLatestPatchAsync()
    {
        if (_desktopInfo == null)
        {
            StatusMessage = "请先检测 GitHub Desktop";
            return;
        }

        if (!await _operationLock.WaitAsync(0))
        {
            StatusMessage = "已有任务正在执行，请稍候";
            AppendLog("下载跳过：已有任务在执行", "WARN");
            return;
        }

        try
        {
            IsBusy = true;
            CurrentStep = "正在获取索引...";
            StatusMessage = "正在连接资源仓库...";
            _logger.Info("Downloading latest patch");

            _patchIndex = await _patchIndexService.LoadIndexAsync();
            if (_patchIndex == null || _patchIndex.Patches.Length == 0)
            {
                StatusMessage = "无法加载补丁索引或索引为空";
                _logger.Error("Failed to load patch index");
                return;
            }

            CurrentStep = "正在查找最新补丁...";
            PatchEntry? latest = null;
            foreach (var p in _patchIndex.Patches)
            {
                if (latest == null) { latest = p; continue; }
                if (Version.TryParse(p.Version, out var pv) && Version.TryParse(latest.Version, out var lv) && pv > lv)
                    latest = p;
            }

            if (latest == null)
            {
                StatusMessage = "索引中没有可用补丁";
                return;
            }

            if (string.Equals(latest.Version, _desktopInfo.Version, StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = $"最新补丁 {latest.Version} 与当前 Desktop 版本一致，可直接使用一键汉化";
                AvailablePatchInfo = $"版本 {latest.Version}  |  大小 {FormatSize(latest.Size)}  |  SHA-256 已校验";
                return;
            }

            CurrentStep = $"正在下载最新补丁 v{latest.Version}...";
            StatusMessage = $"正在下载最新补丁 {latest.Version}（暂不应用，仅预下载）...";
            _logger.Info($"Downloading latest patch {latest.Version}");

            var cacheDirectory = Path.Combine(_dataDirectory, "cache");
            var result = await _downloadService.DownloadPatchAsync(latest, cacheDirectory);

            if (!result.Success)
            {
                StatusMessage = $"下载失败: {result.Error}";
                _logger.Error($"Download failed: {result.Error}");
                return;
            }

            StatusMessage = $"已下载最新补丁 v{latest.Version}（预下载完成，版本匹配后可直接汉化）";
            AvailablePatchInfo = $"最新补丁 {latest.Version} 已缓存  |  大小 {FormatSize(latest.Size)}  |  SHA-256 已校验";
            _logger.Info($"Downloaded latest patch {latest.Version} to {result.FilePath}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"下载失败: {ex.Message}";
            _logger.Error($"Download latest patch failed: {ex.Message}");
        }
        finally
        {
            CurrentStep = string.Empty;
            IsBusy = false;
            _operationLock.Release();
        }
    }

    public async Task CheckAppUpdatesAsync()
    {
        if (!await _operationLock.WaitAsync(0))
        {
            StatusMessage = "已有任务正在执行，请稍候";
            return;
        }

        try
        {
            IsBusy = true;
            CurrentStep = "正在检查本软件更新...";
            StatusMessage = "正在检查本软件更新...";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            AppendLog("[Update] Start checking GitHubDesktopZh update");

            var currentVersion = ParseVersion(
                System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3));
            AppendLog($"[Update] Current version: {currentVersion?.ToString() ?? "unknown"}");

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GitHubDesktopZh");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            // 1. Try /releases/latest
            var latestTag = await FetchLatestReleaseAsync(http);
            if (latestTag != null)
            {
                var remoteVersion = ParseVersion(latestTag.tagName);
                AppendLog($"[Update] Remote version: {remoteVersion?.ToString() ?? "unknown"}");
                AppendLog($"[Update] Elapsed: {sw.ElapsedMilliseconds} ms");

                if (remoteVersion != null && currentVersion != null)
                {
                    var cmp = remoteVersion.CompareTo(currentVersion);
                    if (cmp > 0)
                    {
                        StatusMessage = $"发现新版本 {latestTag.tagName}";
                        AvailablePatchInfo = $"发现新版本: {latestTag.tagName}（当前: v{currentVersion}）\n{latestTag.body}";
                        AppendLog("[Update] Result: NewVersion");
                    }
                    else if (cmp == 0)
                    {
                        StatusMessage = $"当前已是最新版本 v{currentVersion}";
                        AvailablePatchInfo = $"本软件版本: v{currentVersion}（已是最新）";
                        AppendLog("[Update] Result: Latest");
                    }
                    else
                    {
                        StatusMessage = $"当前安装版本 v{currentVersion} 高于公开版本 {latestTag.tagName}";
                        AvailablePatchInfo = $"当前版本: v{currentVersion}，公开版本: {latestTag.tagName}\n无需更新";
                        AppendLog("[Update] Result: LocalNewer");
                    }
                }
                else
                {
                    StatusMessage = $"发现新版本 {latestTag.tagName}";
                    AvailablePatchInfo = $"发现新版本: {latestTag.tagName}（当前: v{currentVersion?.ToString() ?? "unknown"}）\n{latestTag.body}";
                    AppendLog("[Update] Result: NewVersion");
                }

                await _stateManager.UpdateLastCheckTimeAsync();
                LastCheckTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                return;
            }

            // 2. /releases/latest returned 404, try /releases list
            AppendLog("[Update] /releases/latest not found, querying /releases list");
            var releases = await FetchReleasesListAsync(http);
            var stable = releases
                .Where(r => !r.draft && !r.prerelease)
                .OrderByDescending(r => ParseVersion(r.tagName) ?? new Version(0, 0, 0))
                .FirstOrDefault();

            if (stable == null)
            {
                StatusMessage = "未找到 GitHubDesktopZh 的正式发布版本";
                AvailablePatchInfo = "当前仓库还没有正式发布版本";
                AppendLog("[Update] Result: NoRelease");
                AppendLog($"[Update] Elapsed: {sw.ElapsedMilliseconds} ms");

                await _stateManager.UpdateLastCheckTimeAsync();
                LastCheckTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                return;
            }

            var stableVersion = ParseVersion(stable.tagName);
            AppendLog($"[Update] Remote version: {stableVersion?.ToString() ?? "unknown"}");
            AppendLog($"[Update] Elapsed: {sw.ElapsedMilliseconds} ms");

            if (stableVersion != null && currentVersion != null)
            {
                var cmp = stableVersion.CompareTo(currentVersion);
                if (cmp > 0)
                {
                    StatusMessage = $"发现新版本 {stable.tagName}";
                    AvailablePatchInfo = $"发现新版本: {stable.tagName}（当前: v{currentVersion}）\n{stable.body}";
                    AppendLog("[Update] Result: NewVersion");
                }
                else if (cmp == 0)
                {
                    StatusMessage = $"当前已是最新版本 v{currentVersion}";
                    AvailablePatchInfo = $"本软件版本: v{currentVersion}（已是最新）";
                    AppendLog("[Update] Result: Latest");
                }
                else
                {
                    StatusMessage = $"当前安装版本 v{currentVersion} 高于公开版本 {stable.tagName}";
                    AvailablePatchInfo = $"当前版本: v{currentVersion}，公开版本: {stable.tagName}\n无需更新";
                    AppendLog("[Update] Result: LocalNewer");
                }
            }
            else
            {
                StatusMessage = $"发现新版本 {stable.tagName}";
                AvailablePatchInfo = $"发现新版本: {stable.tagName}（当前: v{currentVersion?.ToString() ?? "unknown"}）\n{stable.body}";
                AppendLog("[Update] Result: NewVersion");
            }

            await _stateManager.UpdateLastCheckTimeAsync();
            LastCheckTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "连接 GitHub 超时";
            _logger.Error("Check app updates timed out");
            AppendLog("[Update] Result: Timeout", "ERROR");
        }
        catch (HttpRequestException ex)
        {
            var msg = ex.StatusCode switch
            {
                System.Net.HttpStatusCode.Forbidden => "GitHub API 请求受限，稍后重试",
                System.Net.HttpStatusCode.BadGateway or System.Net.HttpStatusCode.ServiceUnavailable
                    or System.Net.HttpStatusCode.GatewayTimeout => "GitHub 服务暂时不可用",
                _ => $"无法连接 GitHub，请检查网络、代理或 VPN 设置"
            };
            StatusMessage = msg;
            _logger.Error($"Check app updates failed: {ex.Message}");
            AppendLog($"[Update] Result: NetworkError - {ex.Message}", "ERROR");
        }
        catch (Exception ex)
        {
            StatusMessage = $"检查本软件更新失败: {ex.Message}";
            _logger.Error($"Check app updates failed: {ex.Message}");
            AppendLog($"[Update] Result: Error - {ex.Message}", "ERROR");
        }
        finally
        {
            CurrentStep = string.Empty;
            IsBusy = false;
            _operationLock.Release();
        }
    }

    private async Task<ReleaseInfo?> FetchLatestReleaseAsync(HttpClient http)
    {
        try
        {
            var response = await http.GetAsync("https://api.github.com/repos/yuzai114514/GitHubDesktopZh/releases/latest");
            AppendLog($"[Update] HTTP status: {(int)response.StatusCode}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new ReleaseInfo
            {
                tagName = root.GetProperty("tag_name").GetString() ?? "",
                body = root.GetProperty("body").GetString() ?? "",
                draft = root.TryGetProperty("draft", out var d) && d.GetBoolean(),
                prerelease = root.TryGetProperty("prerelease", out var p) && p.GetBoolean()
            };
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidOperationException($"GitHub 返回的更新信息无法解析: {ex.Message}", ex);
        }
    }

    private async Task<List<ReleaseInfo>> FetchReleasesListAsync(HttpClient http)
    {
        try
        {
            var response = await http.GetAsync("https://api.github.com/repos/yuzai114514/GitHubDesktopZh/releases?per_page=20");
            AppendLog($"[Update] /releases HTTP status: {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
                return new List<ReleaseInfo>();

            var json = await response.Content.ReadAsStringAsync();
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var result = new List<ReleaseInfo>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                result.Add(new ReleaseInfo
                {
                    tagName = item.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "",
                    body = item.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "",
                    draft = item.TryGetProperty("draft", out var d) && d.GetBoolean(),
                    prerelease = item.TryGetProperty("prerelease", out var p) && p.GetBoolean()
                });
            }

            return result;
        }
        catch
        {
            return new List<ReleaseInfo>();
        }
    }

    private static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value[1..];

        var plusIndex = value.IndexOf('+');
        if (plusIndex >= 0)
            value = value[..plusIndex];

        var dashIndex = value.IndexOf('-');
        if (dashIndex >= 0)
            value = value[..dashIndex];

        return Version.TryParse(value, out var version) ? version : null;
    }

    private class ReleaseInfo
    {
        public string tagName { get; set; } = "";
        public string body { get; set; } = "";
        public bool draft { get; set; }
        public bool prerelease { get; set; }
    }

    private Manifest? LoadManifestFromZip(string filePath)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string? manifestJson = null;

            if (ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz")
            {
                using var fileStream = File.OpenRead(filePath);
                using var archive = ArchiveFactory.Open(fileStream);
                var entry = archive.Entries.FirstOrDefault(e => e.Key != null && e.Key.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    using var entryStream = entry.OpenEntryStream();
                    using var sr = new StreamReader(entryStream);
                    manifestJson = sr.ReadToEnd();
                }
            }
            else
            {
                using var archive = ZipFile.OpenRead(filePath);
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null) return null;
                using var stream = manifestEntry.Open();
                using var reader = new StreamReader(stream);
                manifestJson = reader.ReadToEnd();
            }

            if (manifestJson == null) return null;
            return System.Text.Json.JsonSerializer.Deserialize<Manifest>(manifestJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private Manifest? CreateDefaultManifest(string filePath, string version)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            List<string> files = new();

            if (ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz")
            {
                using var fileStream = File.OpenRead(filePath);
                using var archive = ArchiveFactory.Open(fileStream);
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    var name = Path.GetFileName(entry.Key);
                    if (name != null &&
                        (name.Equals("main.js", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("renderer.js", StringComparison.OrdinalIgnoreCase)))
                    {
                        var relativePath = entry.Key.Replace('\\', '/');
                        var idx = relativePath.LastIndexOf('/');
                        files.Add(idx >= 0 ? relativePath[(idx + 1)..] : relativePath);
                    }
                }
            }
            else
            {
                using var archive = ZipFile.OpenRead(filePath);
                foreach (var entry in archive.Entries)
                {
                    var name = Path.GetFileName(entry.Name);
                    if (name.Equals("main.js", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("renderer.js", StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(name);
                    }
                }
            }

            if (files.Count == 0) return null;

            return new Manifest
            {
                Version = version,
                Files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Allowlist = Array.Empty<string>()
            };
        }
        catch
        {
            return null;
        }
    }

    private void UpdateStartupRegistry(bool enable, bool silent)
    {
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "GitHubDesktopZh";

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key == null)
                return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var args = silent ? " --silent" : "";
                    key.SetValue(valueName, $"\"{exePath}\"{args}");
                }
            }
            else
            {
                key.DeleteValue(valueName, false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update startup registry: {ex.Message}");
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
