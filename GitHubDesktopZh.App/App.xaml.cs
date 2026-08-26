using System.Windows;
using GitHubDesktopZh.App.Services;

namespace GitHubDesktopZh.App;

public partial class App : System.Windows.Application
{
    private TrayIconManager? _trayIconManager;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var isSilent = e.Args.Contains("--silent", StringComparer.OrdinalIgnoreCase);

        _trayIconManager = new TrayIconManager(
            onCheckUpdate: () => _mainWindow?.CheckForUpdate(),
            onRelocalize: () => _mainWindow?.Relocalize(),
            onOpenSettings: () => _mainWindow?.OpenSettings(),
            onOpenLogs: () => OpenLogsDirectory(),
            onExit: () => Shutdown()
        );
        _trayIconManager.Initialize();

        _mainWindow = new MainWindow();
        _mainWindow.Closing += MainWindow_Closing;

        if (!isSilent)
        {
            _mainWindow.Show();
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private void OpenLogsDirectory()
    {
        var logDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GitHubDesktopZh", "logs");
        
        if (!System.IO.Directory.Exists(logDir))
        {
            System.IO.Directory.CreateDirectory(logDir);
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = logDir,
            UseShellExecute = true
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}