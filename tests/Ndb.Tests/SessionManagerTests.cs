using System.Text.Json;
using Ndb.Daemon;

namespace Ndb.Tests;

public class SessionManagerTests : IDisposable
{
    private readonly string _testDir;

    public SessionManagerTests()
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
    public void Save_CreatesSessionFile()
    {
        var manager = new SessionManager(_testDir);
        var info = new SessionInfo { Pid = 1234, Pipe = "ndb-1234", Log = "/tmp/ndb.log" };
        manager.Save(info);
        var path = Path.Combine(_testDir, "sessions", "default.json");
        Assert.True(File.Exists(path));
        var loaded = JsonSerializer.Deserialize<SessionInfo>(File.ReadAllText(path));
        Assert.Equal(1234, loaded!.Pid);
        Assert.Equal("ndb-1234", loaded.Pipe);
    }

    [Fact]
    public void Load_ReturnsSessionInfo()
    {
        var manager = new SessionManager(_testDir);
        var info = new SessionInfo { Pid = 5678, Pipe = "ndb-5678", Log = "/tmp/ndb.log" };
        manager.Save(info);
        var loaded = manager.Load();
        Assert.NotNull(loaded);
        Assert.Equal(5678, loaded!.Pid);
    }

    [Fact]
    public void Load_ReturnsNull_WhenNoSession()
    {
        var manager = new SessionManager(_testDir);
        Assert.Null(manager.Load());
    }

    [Fact]
    public void Delete_RemovesSessionFile()
    {
        var manager = new SessionManager(_testDir);
        manager.Save(new SessionInfo { Pid = 1, Pipe = "p", Log = "l" });
        manager.Delete();
        Assert.Null(manager.Load());
    }

    [Fact]
    public void IsProcessAlive_ReturnsFalse_ForInvalidPid()
    {
        Assert.False(SessionManager.IsProcessAlive(999999));
    }
}
