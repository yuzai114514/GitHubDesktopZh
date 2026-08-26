using System.IO;
using System.IO.Compression;
using System.Net.Http;
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
        _backupManager = new BackupManager(_dataDirectory);
        _stateManager = new StateManager(Path.Combine(_dataDirectory, "state.json"));
        _settingsManager = new SettingsManager(Path.Combine(_dataDirectory, "settings.json"));
        _logger = new Logger(Path.Combine(_dataDirectory, "logs"));

        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
        LocalizeCommand = new RelayCommand(async () => await LocalizeAsync());
        RestoreCommand = new RelayCommand(async () => await RestoreAsync());
        DownloadLatestCommand = new RelayCommand(async () => await DownloadLatestPatchAsync());
        CheckAppUpdatesCommand = new RelayCommand(async () => await CheckAppUpdatesAsync());
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
                _settingsManager.SaveSettingsAsync(_settings);
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
                _settingsManager.SaveSettingsAsync(_settings);
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
                _settingsManager.SaveSettingsAsync(_settings);
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
                _settingsManager.SaveSettingsAsync(_settings);
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
                _settingsManager.SaveSettingsAsync(_settings);
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
                _settingsManager.SaveSettingsAsync(_settings);
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
                _settingsManager.SaveSettingsAsync(_settings);
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

    public RelayCommand CheckForUpdatesCommand { get; } = null!;
    public RelayCommand LocalizeCommand { get; } = null!;
    public RelayCommand RestoreCommand { get; } = null!;
    public RelayCommand DownloadLatestCommand { get; } = null!;
    public RelayCommand CheckAppUpdatesCommand { get; } = null!;

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

        // Use the user-configured index URL (falls back to cache/bundled index on failure)
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var urls = _settings.IndexUrl
            .Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();
        if (urls.Count == 0)
            urls.Add(DefaultIndexUrl);
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
        {
            LocalizedVersion = _state.LocalizedVersion;
        }
        else
        {
            LocalizedVersion = string.Empty;
        }

        if (_state?.LastCheckTime != null)
        {
            LastCheckTime = _state.LastCheckTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (_state?.LastOperationTime != null)
        {
            LastOperationTime = _state.LastOperationTime.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        if (_desktopInfo == null)
        {
            StatusMessage = "请先检测 GitHub Desktop";
            return;
        }

        IsBusy = true;
        CurrentStep = "正在连接资源仓库...";
        StatusMessage = "正在检查更新...";
        _logger.Info("Checking for updates");
        AppendLog("开始检查更新");

        try
        {
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

            // 列出所有可用版本
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
                // 没有精确匹配，查找最接近的可用补丁
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
            // 没有精确匹配，查找最接近的可用补丁（向下兼容）
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
            StatusMessage = $"将尝试安装补丁版本 {patch.Version}（向下兼容）";
            AppendLog($"选择兼容补丁版本 {patch.Version}");
        }

        IsBusy = true;

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
                // 没有 manifest.json，尝试直接列出压缩包中的文件（社区格式）
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
                // 向下兼容：补丁版本与 Desktop 版本不一致时仅警告，不阻止导入
                StatusMessage = $"警告: 补丁版本 ({manifest.Version}) 与 Desktop 版本 ({_desktopInfo.Version}) 不一致，尝试兼容安装...";
                _logger.Warning($"Manifest version {manifest.Version} != desktop version {_desktopInfo.Version}, attempting compatible install");
                AppendLog($"版本不一致: 补丁 {manifest.Version} ≠ Desktop {_desktopInfo.Version}，尝试兼容安装", "WARN");
            }

            // Step 3: Backup
            CurrentStep = "③ 备份原始文件...";
            StatusMessage = "正在备份文件...";
            _logger.Info("Backing up files");
            AppendLog("开始备份原始文件");
            await _backupManager.BackupFilesAsync(_desktopInfo, manifest);
            AppendLog("备份完成");

            // Step 4: Import
            CurrentStep = "④ 导入汉化文件...";
            StatusMessage = "正在导入文件...";
            _logger.Info("Importing files");
            AppendLog("开始导入汉化文件");
            await _backupManager.ImportFilesAsync(_desktopInfo, result.FilePath, manifest);
            AppendLog("汉化文件导入完成");

            // Step 4.5: Ensure git\bin\git.exe exists
            CurrentStep = "④⑤ 检查 git 路径...";
            _backupManager.EnsureGitBinPath(_desktopInfo);
            AppendLog("git 路径检查完成");

            // Step 5: Verify
            CurrentStep = "⑤ 验证文件完整性...";
            StatusMessage = "正在验证...";
            if (!_backupManager.VerifyFiles(_desktopInfo, manifest))
            {
                StatusMessage = "文件验证失败，正在恢复...";
                _logger.Error("File verification failed, restoring");
                AppendLog("文件验证失败，正在回滚", "ERROR");
                _backupManager.RestoreFiles(_desktopInfo);
                StatusMessage = "已恢复到备份状态";
                AppendLog("已恢复到备份状态");
                return;
            }
            AppendLog("文件验证通过");

            // Step 6: Done
            CurrentStep = "⑥ 汉化完成！";
            StatusMessage = "汉化完成";
            StatusColor = "Green";
            _logger.Info("Localization completed");
            AppendLog("汉化完成！");

            await _stateManager.UpdateLocalizedVersionAsync(_desktopInfo.Version);
            LocalizedVersion = _desktopInfo.Version;

            await _stateManager.UpdateLastCheckTimeAsync();
            LastOperationTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        finally
        {
            IsBusy = false;
            CurrentStep = string.Empty;
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

        IsBusy = true;
        StatusMessage = "正在恢复...";
        CurrentStep = "正在恢复原始文件...";
        _logger.Info("Restoring files");
        AppendLog("开始恢复原始文件");

        try
        {
            var success = _backupManager.RestoreFiles(_desktopInfo);
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
        }
    }

    public async Task DownloadLatestPatchAsync()
    {
        if (_desktopInfo == null)
        {
            StatusMessage = "请先检测 GitHub Desktop";
            return;
        }

        IsBusy = true;
        CurrentStep = "正在获取索引...";
        StatusMessage = "正在连接资源仓库...";
        _logger.Info("Downloading latest patch");

        try
        {
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
        }
    }

    public async Task CheckAppUpdatesAsync()
    {
        IsBusy = true;
        CurrentStep = "正在检查本软件更新...";
        StatusMessage = "正在检查本软件更新...";
        AppendLog("开始检查本软件更新");

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GitHubDesktopZh");
            var response = await http.GetAsync("https://api.github.com/repos/yuzai114514/GitHubDesktopZh/releases/latest");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                StatusMessage = "暂无本软件更新（仓库暂无 Release）";
                AppendLog("GitHub 仓库暂无 Release，跳过检查");
                return;
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var latestTag = root.GetProperty("tag_name").GetString() ?? "";
            var body = root.GetProperty("body").GetString() ?? "";

            // 获取当前版本
            var currentVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

            AppendLog($"当前版本: {currentVersion}，最新版本: {latestTag}");

            if (string.Equals(currentVersion.TrimStart('v'), latestTag.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = $"当前已是最新版本 {currentVersion}";
                AvailablePatchInfo = $"本软件版本: {currentVersion}（已是最新）";
                AppendLog("本软件已是最新版本");
            }
            else
            {
                StatusMessage = $"发现新版本 {latestTag}";
                AvailablePatchInfo = $"发现新版本: {latestTag}（当前: {currentVersion}）\n{body}";
                AppendLog($"发现新版本: {latestTag}");
            }

            await _stateManager.UpdateLastCheckTimeAsync();
            LastCheckTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (Exception ex)
        {
            StatusMessage = $"检查本软件更新失败: {ex.Message}";
            _logger.Error($"Check app updates failed: {ex.Message}");
            AppendLog($"检查本软件更新失败: {ex.Message}", "ERROR");
        }
        finally
        {
            CurrentStep = string.Empty;
            IsBusy = false;
        }
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
                var entry = archive.Entries.FirstOrDefault(e => e.Key.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase));
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
                    if (name.Equals("main.js", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("renderer.js", StringComparison.OrdinalIgnoreCase))
                    {
                        // 保留相对路径结构
                        var relativePath = entry.Key.Replace('\\', '/');
                        // 去掉可能的前缀目录 (如 Version/3.6.4/Windows/)
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