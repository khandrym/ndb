using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ndb.Json;

namespace Ndb.Daemon;

public class SessionManager
{
    private readonly string _ndbDir;
    private readonly string _sessionsDir;
    private readonly string _logsDir;

    public SessionManager(string? ndbDir = null)
    {
        _ndbDir = ndbDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ndb");
        _sessionsDir = Path.Combine(_ndbDir, "sessions");
        _logsDir = Path.Combine(_ndbDir, "logs");
    }

    public string LogsDir => _logsDir;

    private string SessionPath(string name) => Path.Combine(_sessionsDir, $"{name}.json");

    public SessionInfo? Load(string name = "default")
    {
        var path = SessionPath(name);
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, NdbJsonContext.Default.SessionInfo);
    }

    public void Save(string name, SessionInfo info)
    {
        Directory.CreateDirectory(_sessionsDir);
        var json = JsonSerializer.Serialize(info, NdbJsonContext.Default.SessionInfo);
        File.WriteAllText(SessionPath(name), json);
    }

    // Backward-compatible overload
    public void Save(SessionInfo info)
    {
        Save("default", info);
    }

    public void Delete(string name = "default")
    {
        var path = SessionPath(name);
        if (File.Exists(path))
            File.Delete(path);
    }

    public SessionInfo? LoadAndVerify(string name = "default")
    {
        var info = Load(name);
        if (info is null) return null;
        if (IsProcessAlive(info.Pid))
            return info;
        Delete(name);
        return null;
    }

    public Dictionary<string, SessionInfo> LoadAll()
    {
        var result = new Dictionary<string, SessionInfo>();
        if (!Directory.Exists(_sessionsDir)) return result;

        foreach (var file in Directory.GetFiles(_sessionsDir, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var json = File.ReadAllText(file);
                var info = JsonSerializer.Deserialize(json, NdbJsonContext.Default.SessionInfo);
                if (info is not null)
                    result[name] = info;
            }
            catch { }
        }
        return result;
    }

    public void MigrateIfNeeded()
    {
        var oldPath = Path.Combine(_ndbDir, "session.json");
        if (!File.Exists(oldPath)) return;

        try
        {
            var json = File.ReadAllText(oldPath);
            var info = JsonSerializer.Deserialize(json, NdbJsonContext.Default.SessionInfo);
            if (info is not null)
            {
                Save("default", info);
            }
            File.Delete(oldPath);
        }
        catch { }
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
