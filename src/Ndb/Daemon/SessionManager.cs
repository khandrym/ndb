using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ndb.Daemon;

public class SessionManager
{
    private readonly string _ndbDir;
    private readonly string _sessionPath;
    private readonly string _logsDir;

    public SessionManager(string? ndbDir = null)
    {
        _ndbDir = ndbDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ndb");
        _sessionPath = Path.Combine(_ndbDir, "session.json");
        _logsDir = Path.Combine(_ndbDir, "logs");
    }

    public string LogsDir => _logsDir;

    public SessionInfo? Load()
    {
        if (!File.Exists(_sessionPath)) return null;
        var json = File.ReadAllText(_sessionPath);
        return JsonSerializer.Deserialize<SessionInfo>(json);
    }

    public void Save(SessionInfo info)
    {
        Directory.CreateDirectory(_ndbDir);
        var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_sessionPath, json);
    }

    public void Delete()
    {
        if (File.Exists(_sessionPath))
            File.Delete(_sessionPath);
    }

    public SessionInfo? LoadAndVerify()
    {
        var info = Load();
        if (info is null) return null;
        if (IsProcessAlive(info.Pid))
            return info;
        Delete();
        return null;
    }

    public static bool IsProcessAlive(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch { return false; }
    }

    public void RotateLogs(int keepCount = 5)
    {
        if (!Directory.Exists(_logsDir)) return;
        var files = Directory.GetFiles(_logsDir, "*.log")
            .OrderByDescending(f => f)
            .Skip(keepCount)
            .ToList();
        foreach (var file in files)
        {
            try { File.Delete(file); } catch { }
        }
    }

    public string CreateLogPath()
    {
        Directory.CreateDirectory(_logsDir);
        var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss");
        return Path.Combine(_logsDir, $"{timestamp}.log");
    }
}
