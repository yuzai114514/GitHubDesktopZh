using System.Diagnostics;

namespace GitHubDesktopZh.Core.Services;

public class DesktopProcessService
{
    private const string ProcessName = "GitHubDesktop";

    public bool IsRunning()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        return processes.Length > 0;
    }

    public async Task<bool> CloseAndWaitAsync(int timeoutMs = 5000)
    {
        var processes = Process.GetProcessesByName(ProcessName);
        if (processes.Length == 0)
            return true;

        foreach (var proc in processes)
        {
            try
            {
                proc.CloseMainWindow();
            }
            catch
            {
                // 进程可能已退出
            }
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            processes = Process.GetProcessesByName(ProcessName);
            if (processes.Length == 0)
                return true;
            await Task.Delay(200);
        }

        // 超时后强制终止
        processes = Process.GetProcessesByName(ProcessName);
        foreach (var proc in processes)
        {
            try
            {
                proc.Kill();
                proc.WaitForExit(2000);
            }
            catch
            {
                // 忽略
            }
        }

        processes = Process.GetProcessesByName(ProcessName);
        return processes.Length == 0;
    }

    public void KillAll()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        foreach (var proc in processes)
        {
            try
            {
                proc.Kill();
                proc.WaitForExit(2000);
            }
            catch
            {
                // 忽略
            }
        }
    }
}
