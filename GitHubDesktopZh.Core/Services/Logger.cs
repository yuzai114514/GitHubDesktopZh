namespace GitHubDesktopZh.Core.Services;

public class Logger
{
    private readonly string _logDirectory;
    private readonly object _lock = new();

    public Logger(string logDirectory)
    {
        _logDirectory = logDirectory;
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        var logFile = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] {message}";

        lock (_lock)
        {
            File.AppendAllText(logFile, logEntry + Environment.NewLine);
        }
    }

    public void Info(string message) => Log(message, LogLevel.Info);
    public void Warning(string message) => Log(message, LogLevel.Warning);
    public void Error(string message) => Log(message, LogLevel.Error);
    public void Debug(string message) => Log(message, LogLevel.Debug);
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}