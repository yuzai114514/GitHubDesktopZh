using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GitHubDesktopZh.App.Services;

public class TrayIconManager : IDisposable
{
    private NotifyIcon? _trayIcon;
    private readonly Action _onCheckUpdate;
    private readonly Action _onRelocalize;
    private readonly Action _onOpenSettings;
    private readonly Action _onOpenLogs;
    private readonly Action _onExit;

    public TrayIconManager(
        Action onCheckUpdate,
        Action onRelocalize,
        Action onOpenSettings,
        Action onOpenLogs,
        Action onExit)
    {
        _onCheckUpdate = onCheckUpdate;
        _onRelocalize = onRelocalize;
        _onOpenSettings = onOpenSettings;
        _onOpenLogs = onOpenLogs;
        _onExit = onExit;
    }

    public void Initialize()
    {
        _trayIcon = new NotifyIcon
        {
            Text = "GitHub Desktop 中文助手",
            Visible = true,
            Icon = SystemIcons.Application
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("显示主窗口", null, (s, e) => ShowMainWindow());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("检查更新", null, (s, e) => _onCheckUpdate());
        contextMenu.Items.Add("重新汉化", null, (s, e) => _onRelocalize());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("打开设置", null, (s, e) => _onOpenSettings());
        contextMenu.Items.Add("打开日志", null, (s, e) => _onOpenLogs());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (s, e) => _onExit());

        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (s, e) => ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            if (window is MainWindow mainWindow)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                break;
            }
        }
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
    }
}