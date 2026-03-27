using System;
using System.IO;

namespace Ndb.Daemon;

public enum LogLevel { Error, Info, Debug }

public sealed class FileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly LogLevel _minLevel;
    private readonly object _lock = new();

    public FileLogger(string path, bool verbose = false)
    {
        _minLevel = verbose ? LogLevel.Debug : LogLevel.Error;
        _writer = new StreamWriter(path, append: true) { AutoFlush = true };
    }

    public void Log(LogLevel level, string message)
    {
        if (level > _minLevel) return;
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level}] {message}";
        lock (_lock) { _writer.WriteLine(line); }
    }

    public void Error(string message) => Log(LogLevel.Error, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Dispose() => _writer.Dispose();
}
