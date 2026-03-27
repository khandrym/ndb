using System;
using System.Collections.Generic;
using System.Linq;

namespace Ndb.Daemon;

public class ManagedBreakpoint
{
    public int Id { get; set; }
    public string File { get; set; } = "";
    public int Line { get; set; }
    public string? Condition { get; set; }
    public string? LogMessage { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Verified { get; set; }
    public string? Message { get; set; }
    public bool IsTemporary { get; set; }
}

public class BreakpointManager
{
    private readonly Dictionary<string, List<ManagedBreakpoint>> _breakpoints = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId;

    public ManagedBreakpoint Add(string file, int line, string? condition = null, string? logMessage = null)
    {
        if (!_breakpoints.TryGetValue(file, out var list))
        {
            list = new List<ManagedBreakpoint>();
            _breakpoints[file] = list;
        }

        var existing = list.FirstOrDefault(bp => bp.Line == line);
        if (existing is not null)
            return existing;

        var bp = new ManagedBreakpoint
        {
            Id = ++_nextId,
            File = file,
            Line = line,
            Condition = condition,
            LogMessage = logMessage
        };
        list.Add(bp);
        return bp;
    }

    public bool Remove(string file, int line)
    {
        if (!_breakpoints.TryGetValue(file, out var list))
            return false;

        var removed = list.RemoveAll(bp => bp.Line == line) > 0;
        if (list.Count == 0)
            _breakpoints.Remove(file);
        return removed;
    }

    public ManagedBreakpoint? FindById(int id)
    {
        foreach (var list in _breakpoints.Values)
        {
            var bp = list.FirstOrDefault(b => b.Id == id);
            if (bp is not null) return bp;
        }
        return null;
    }

    public bool Enable(int id)
    {
        var bp = FindById(id);
        if (bp is null) return false;
        bp.Enabled = true;
        return true;
    }

    public bool Disable(int id)
    {
        var bp = FindById(id);
        if (bp is null) return false;
        bp.Enabled = false;
        return true;
    }

    public List<ManagedBreakpoint> GetAll()
    {
        return _breakpoints.Values.SelectMany(l => l).ToList();
    }

    public List<ManagedBreakpoint> GetEnabledForFile(string file)
    {
        if (!_breakpoints.TryGetValue(file, out var list))
            return new List<ManagedBreakpoint>();
        return list.Where(bp => bp.Enabled).ToList();
    }

    public List<string> GetFilesWithBreakpoints()
    {
        return _breakpoints.Keys.ToList();
    }

    public ManagedBreakpoint AddTemporary(string file, int line)
    {
        var bp = Add(file, line);
        bp.IsTemporary = true;
        return bp;
    }

    public void RemoveTemporary()
    {
        foreach (var file in _breakpoints.Keys.ToList())
        {
            var list = _breakpoints[file];
            list.RemoveAll(bp => bp.IsTemporary);
            if (list.Count == 0)
                _breakpoints.Remove(file);
        }
    }

    public void UpdateVerified(string file, int line, bool verified)
    {
        if (!_breakpoints.TryGetValue(file, out var list)) return;
        var bp = list.FirstOrDefault(b => b.Line == line);
        if (bp is not null) bp.Verified = verified;
    }

    public void UpdateMessage(string file, int line, string? message)
    {
        if (!_breakpoints.TryGetValue(file, out var list)) return;
        var bp = list.FirstOrDefault(b => b.Line == line);
        if (bp is not null) bp.Message = message;
    }

    public string? GetFileForBreakpoint(int id)
    {
        var bp = FindById(id);
        return bp?.File;
    }
}
