using System.Text.Json;
using Ndb.Daemon;
using Ndb.Dap.Messages;
using Ndb.Ipc;

namespace Ndb.Tests;

public class RequestDispatcherTests : IDisposable
{
    private readonly string _logFile;
    private readonly FileLogger _logger;
    private readonly FakeDapClient _fake;
    private readonly RequestDispatcher _dispatcher;

    public RequestDispatcherTests()
    {
        _logFile = Path.GetTempFileName();
        _logger = new FileLogger(_logFile);
        _fake = new FakeDapClient();
        _dispatcher = new RequestDispatcher(_fake, _logger);
    }

    public void Dispose()
    {
        _logger.Dispose();
        if (File.Exists(_logFile))
            File.Delete(_logFile);
    }

    // Helper: build a DapEvent for "stopped" with a threadId
    private static DapEvent MakeStoppedEvent(int threadId)
    {
        var body = JsonSerializer.SerializeToElement(new { reason = "breakpoint", threadId });
        return new DapEvent { Event = "stopped", Body = body };
    }

    // Helper: build a ThreadsResponse with specified thread IDs
    private static DapResponse MakeThreadsResponse(params int[] threadIds)
    {
        var threads = threadIds.Select(id => new { id, name = $"Thread {id}" }).ToArray();
        var body = JsonSerializer.SerializeToElement(new { threads });
        return new DapResponse { Success = true, Body = body };
    }

    // Helper: create an IpcRequest with reflection-serialized params
    private static IpcRequest Req(int id, string method, object? @params = null)
    {
        JsonElement? paramsElement = null;
        if (@params is not null)
            paramsElement = JsonSerializer.SerializeToElement(@params);
        return new IpcRequest { Id = id, Method = method, Params = paramsElement };
    }

    // Helper: do a launch to initialize the dispatcher session
    private async Task LaunchAsync()
    {
        var req = Req(1, "launch", new { program = "/fake/app" });
        await _dispatcher.DispatchAsync(req, default);
    }

    // Helper: do an attach to initialize the dispatcher session as attached
    private async Task AttachAsync()
    {
        var req = Req(1, "attach", new { processId = 123 });
        await _dispatcher.DispatchAsync(req, default);
    }

    // Test 1: pause with explicit threadId=5 → PauseAsync called with 5
    [Fact]
    public async Task Pause_WithExplicitThread_UsesExplicitThread()
    {
        await LaunchAsync();

        var req = Req(2, "exec.pause", new { threadId = 5 });
        var response = await _dispatcher.DispatchAsync(req, default);

        Assert.Null(response.Error);
        Assert.Equal(5, _fake.LastPauseThreadId);
    }

    // Test 2: pause with no threadId, but lastStoppedThreadId=42 from prior stopped event
    [Fact]
    public async Task Pause_NoThread_UsesLastStoppedThread()
    {
        await LaunchAsync();

        // Trigger a stopped event via continue --wait so _lastStoppedThreadId gets set to 42
        _fake.NextStoppedEvent = MakeStoppedEvent(42);
        var continueReq = Req(2, "exec.continue", new { wait = true, timeout = 5 });
        await _dispatcher.DispatchAsync(continueReq, default);

        // Now pause without specifying threadId
        var pauseReq = Req(3, "exec.pause");
        await _dispatcher.DispatchAsync(pauseReq, default);

        Assert.Equal(42, _fake.LastPauseThreadId);
    }

    // Test 3: no lastStopped, ThreadsResponse has [100, 200] → uses 100
    [Fact]
    public async Task Pause_NoThread_FallsBackToFirstThread()
    {
        await LaunchAsync();

        _fake.ThreadsResponse = MakeThreadsResponse(100, 200);

        var pauseReq = Req(2, "exec.pause");
        await _dispatcher.DispatchAsync(pauseReq, default);

        Assert.Equal(100, _fake.LastPauseThreadId);
    }

    // Test 4: no lastStopped, empty ThreadsResponse → sends 0
    [Fact]
    public async Task Pause_NoThreads_SendsZero()
    {
        await LaunchAsync();

        _fake.ThreadsResponse = MakeThreadsResponse(); // empty

        var pauseReq = Req(2, "exec.pause");
        await _dispatcher.DispatchAsync(pauseReq, default);

        Assert.Equal(0, _fake.LastPauseThreadId);
    }

    // Test 5: after continue (no wait), _lastStoppedThreadId is reset → pause falls back to threads
    [Fact]
    public async Task Continue_ResetsLastStoppedThread()
    {
        await LaunchAsync();

        // Set lastStoppedThreadId = 42 via stopped event
        _fake.NextStoppedEvent = MakeStoppedEvent(42);
        var continueWithWait = Req(2, "exec.continue", new { wait = true, timeout = 5 });
        await _dispatcher.DispatchAsync(continueWithWait, default);

        // Now continue without waiting — this resets _lastStoppedThreadId
        var continueNoWait = Req(3, "exec.continue");
        await _dispatcher.DispatchAsync(continueNoWait, default);

        // Set up threads fallback
        _fake.ThreadsResponse = MakeThreadsResponse(99);

        // Pause — should use threads fallback (99), not 42
        var pauseReq = Req(4, "exec.pause");
        await _dispatcher.DispatchAsync(pauseReq, default);

        Assert.Equal(99, _fake.LastPauseThreadId);
    }

    // Test 6: set "all" then "none" → empty array sent
    [Fact]
    public async Task ExceptionFilter_None_ClearsFilters()
    {
        await LaunchAsync();

        // Set filter to "all"
        var setAll = Req(2, "breakpoint.exception", new { filter = "all" });
        await _dispatcher.DispatchAsync(setAll, default);

        Assert.NotNull(_fake.LastExceptionFilters);
        Assert.Contains("all", _fake.LastExceptionFilters!);

        // Set filter to "none" — should clear
        var setNone = Req(3, "breakpoint.exception", new { filter = "none" });
        await _dispatcher.DispatchAsync(setNone, default);

        Assert.NotNull(_fake.LastExceptionFilters);
        Assert.Empty(_fake.LastExceptionFilters!);
    }

    // Test 7: launch then stop → DisconnectAsync called with terminateDebuggee=true
    [Fact]
    public async Task Stop_AfterLaunch_Terminates()
    {
        await LaunchAsync();

        var stopReq = Req(2, "stop");
        await _dispatcher.DispatchAsync(stopReq, default);

        Assert.True(_fake.LastDisconnectTerminate);
    }

    // Test 8: attach then stop → DisconnectAsync called with terminateDebuggee=false
    [Fact]
    public async Task Stop_AfterAttach_Detaches()
    {
        await AttachAsync();

        var stopReq = Req(2, "stop");
        await _dispatcher.DispatchAsync(stopReq, default);

        Assert.False(_fake.LastDisconnectTerminate);
    }

    // Test 9: continue --wait returns stopped(threadId=7), then pause uses 7
    [Fact]
    public async Task StoppedEvent_UpdatesLastStoppedThread()
    {
        await LaunchAsync();

        _fake.NextStoppedEvent = MakeStoppedEvent(7);
        var continueReq = Req(2, "exec.continue", new { wait = true, timeout = 5 });
        await _dispatcher.DispatchAsync(continueReq, default);

        var pauseReq = Req(3, "exec.pause");
        await _dispatcher.DispatchAsync(pauseReq, default);

        Assert.Equal(7, _fake.LastPauseThreadId);
    }
}
