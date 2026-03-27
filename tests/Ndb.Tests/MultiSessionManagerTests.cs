using System;
using System.Collections.Generic;
using System.IO;
using Ndb.Daemon;

namespace Ndb.Tests;

public class MultiSessionManagerTests : IDisposable
{
    private readonly string _testDir;

    public MultiSessionManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"ndb-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Save_NamedSession_CreatesInSessionsDir()
    {
        var mgr = new SessionManager(_testDir);
        var info = new SessionInfo { Pid = 1234, Pipe = "ndb-myapp-1234", Log = "/tmp/ndb.log" };
        mgr.Save("myapp", info);

        var path = Path.Combine(_testDir, "sessions", "myapp.json");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Load_NamedSession_ReturnsCorrectSession()
    {
        var mgr = new SessionManager(_testDir);
        mgr.Save("app1", new SessionInfo { Pid = 111, Pipe = "p1", Log = "l1" });
        mgr.Save("app2", new SessionInfo { Pid = 222, Pipe = "p2", Log = "l2" });

        var s1 = mgr.Load("app1");
        var s2 = mgr.Load("app2");

        Assert.Equal(111, s1!.Pid);
        Assert.Equal(222, s2!.Pid);
    }

    [Fact]
    public void Load_DefaultSession()
    {
        var mgr = new SessionManager(_testDir);
        mgr.Save("default", new SessionInfo { Pid = 999, Pipe = "p", Log = "l" });

        var s = mgr.Load(); // no name = default
        Assert.Equal(999, s!.Pid);
    }

    [Fact]
    public void Delete_NamedSession()
    {
        var mgr = new SessionManager(_testDir);
        mgr.Save("myapp", new SessionInfo { Pid = 1, Pipe = "p", Log = "l" });
        mgr.Delete("myapp");

        Assert.Null(mgr.Load("myapp"));
    }

    [Fact]
    public void LoadAll_ReturnsAllSessions()
    {
        var mgr = new SessionManager(_testDir);
        mgr.Save("app1", new SessionInfo { Pid = 1, Pipe = "p1", Log = "l1" });
        mgr.Save("app2", new SessionInfo { Pid = 2, Pipe = "p2", Log = "l2" });

        var all = mgr.LoadAll();
        Assert.Equal(2, all.Count);
        Assert.True(all.ContainsKey("app1"));
        Assert.True(all.ContainsKey("app2"));
    }

    [Fact]
    public void Migration_OldSessionJson_TreatedAsDefault()
    {
        var mgr = new SessionManager(_testDir);
        // Write old-style session.json
        var oldPath = Path.Combine(_testDir, "session.json");
        File.WriteAllText(oldPath, """{"pid":42,"pipe":"old","log":"old.log"}""");

        mgr.MigrateIfNeeded();

        var s = mgr.Load("default");
        Assert.NotNull(s);
        Assert.Equal(42, s!.Pid);
        Assert.False(File.Exists(oldPath)); // old file removed
    }
}
