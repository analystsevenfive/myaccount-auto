using System;
using System.IO;
using System.Text;

namespace ProsoftAutoLogin.Automation;

public static class FileLogger
{
    private static readonly object _lock = new();
    private static string? _logFilePath;

    public static string LogFilePath
    {
        get
        {
            if (_logFilePath is not null) return _logFilePath;

            var logsDir = ResolveLogsDirectory();
            _logFilePath = Path.Combine(logsDir, "app.log");
            return _logFilePath;
        }
    }

    private static string ResolveLogsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ProsoftAutoLogin.sln")))
            {
                var path = Path.Combine(dir.FullName, "logs");
                try { Directory.CreateDirectory(path); } catch { }
                return path;
            }
            dir = dir.Parent;
        }

        var currentDir = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(currentDir, "ProsoftAutoLogin.sln")))
        {
            var path = Path.Combine(currentDir, "logs");
            try { Directory.CreateDirectory(path); } catch { }
            return path;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "logs");
        try { Directory.CreateDirectory(fallback); } catch { }
        return fallback;
    }

    public static void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(LogFilePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Do not throw on logging errors
        }
    }
}
