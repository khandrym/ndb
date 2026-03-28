# IDapClient Interface & RequestDispatcher Tests — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract `IDapClient` interface from `DapClient` and add unit tests for `RequestDispatcher` thread resolution, exception filter, and stop behavior.

**Architecture:** Extract interface from existing class (mechanical). Create `FakeDapClient` test double that returns configurable responses and tracks calls. Write 9 focused unit tests.

**Tech Stack:** C# / .NET 10, xUnit, no mocking framework

---

## File Structure

### New files

```
src/Ndb.Dap/IDapClient.cs                    # Interface: 18 methods + Events property
tests/Ndb.Tests/FakeDapClient.cs              # Test double implementing IDapClient
tests/Ndb.Tests/RequestDispatcherTests.cs     # 9 unit tests
```

### Modified files

```
src/Ndb.Dap/DapClient.cs                     # Add : IDapClient
src/Ndb/Daemon/RequestDispatcher.cs           # DapClient → IDapClient in field and constructor
```

---

## Task 1: Extract IDapClient interface

**Files:**
- Create: `src/Ndb.Dap/IDapClient.cs`
- Modify: `src/Ndb.Dap/DapClient.cs`

- [ ] **Step 1: Create the interface**

Create `src/Ndb.Dap/IDapClient.cs`:

```csharp
using Ndb.Dap.Messages;

namespace Ndb.Dap;

public interface IDapClient
{
    IReadOnlyList<DapEvent> Events { get; }

    Task<DapResponse> InitializeAsync(CancellationToken ct = default);
    Task<DapResponse> LaunchAsync(LaunchArguments args, CancellationToken ct = default);
    Task<DapResponse> ConfigurationDoneAsync(CancellationToken ct = default);
    Task<DapResponse> DisconnectAsync(bool terminateDebuggee = true, CancellationToken ct = default);
    Task<DapResponse> AttachAsync(int processId, CancellationToken ct = default);
    Task<DapResponse> SetBreakpointsAsync(string filePath, SourceBreakpoint[] breakpoints, CancellationToken ct = default);
    Task<DapResponse> SetExceptionBreakpointsAsync(string[] filters, CancellationToken ct = default);
    Task<DapResponse> ContinueAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> PauseAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> NextAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> StepInAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> StepOutAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> StackTraceAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> ThreadsAsync(CancellationToken ct = default);
    Task<DapResponse> ScopesAsync(int frameId, CancellationToken ct = default);
    Task<DapResponse> VariablesAsync(int variablesReference, CancellationToken ct = default);
    Task<DapResponse> EvaluateAsync(string expression, int frameId, CancellationToken ct = default);
    Task<DapEvent?> WaitForEventAsync(string eventName, TimeSpan timeout, CancellationToken ct = default);
}
```

- [ ] **Step 2: Implement interface on DapClient**

In `src/Ndb.Dap/DapClient.cs`, change line 10 from:

```csharp
public class DapClient : IDisposable
```

to:

```csharp
public class DapClient : IDapClient, IDisposable
```

- [ ] **Step 3: Verify build**

Run: `dotnet build ndb.slnx`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/Ndb.Dap/IDapClient.cs src/Ndb.Dap/DapClient.cs
git commit -m "refactor: extract IDapClient interface from DapClient"
```

---

## Task 2: Update RequestDispatcher to use IDapClient

**Files:**
- Modify: `src/Ndb/Daemon/RequestDispatcher.cs`

- [ ] **Step 1: Change field and constructor types**

In `src/Ndb/Daemon/RequestDispatcher.cs`, change:

```csharp
private readonly DapClient _dap;
```

to:

```csharp
private readonly IDapClient _dap;
```

And change the constructor:

```csharp
public RequestDispatcher(DapClient dap, FileLogger logger, string? logPath = null)
```

to:

```csharp
public RequestDispatcher(IDapClient dap, FileLogger logger, string? logPath = null)
```

- [ ] **Step 2: Verify build**

Run: `dotnet build ndb.slnx`
Expected: 0 errors (DaemonHost passes `DapClient` which implements `IDapClient` — implicit conversion)

- [ ] **Step 3: Run tests**

Run: `dotnet test ndb.slnx --filter "FullyQualifiedName!~IntegrationTests"`
Expected: 40/40 pass, 0 failures

- [ ] **Step 4: Commit**

```bash
git add src/Ndb/Daemon/RequestDispatcher.cs
git commit -m "refactor: use IDapClient in RequestDispatcher for testability"
```

---

## Task 3: Create FakeDapClient test double

**Files:**
- Create: `tests/Ndb.Tests/FakeDapClient.cs`

- [ ] **Step 1: Create FakeDapClient**

Create `tests/Ndb.Tests/FakeDapClient.cs`:

```csharp
using System.Text.Json;
using Ndb.Dap;
using Ndb.Dap.Messages;

namespace Ndb.Tests;

public class FakeDapClient : IDapClient
{
    private readonly List<DapEvent> _events = new();
    public IReadOnlyList<DapEvent> Events => _events;

    // Track calls for assertions
    public bool? LastDisconnectTerminate { get; private set; }
    public string[]? LastExceptionFilters { get; private set; }
    public int LastContinueThreadId { get; private set; }
    public int LastPauseThreadId { get; private set; }
    public int LastNextThreadId { get; private set; }
    public int LastStackTraceThreadId { get; private set; }

    // Configurable responses
    public DapResponse DefaultResponse { get; set; } = new() { Success = true };
    public DapResponse? ThreadsResponse { get; set; }
    public DapResponse? StackTraceResponse { get; set; }

    // Helper to add events (simulates DAP events from netcoredbg)
    public void AddEvent(DapEvent evt) => _events.Add(evt);

    public Task<DapResponse> InitializeAsync(CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapResponse> LaunchAsync(LaunchArguments args, CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapResponse> ConfigurationDoneAsync(CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapResponse> DisconnectAsync(bool terminateDebuggee = true, CancellationToken ct = default)
    {
        LastDisconnectTerminate = terminateDebuggee;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> AttachAsync(int processId, CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapResponse> SetBreakpointsAsync(string filePath, SourceBreakpoint[] breakpoints, CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapResponse> SetExceptionBreakpointsAsync(string[] filters, CancellationToken ct = default)
    {
        LastExceptionFilters = filters;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> ContinueAsync(int threadId, CancellationToken ct = default)
    {
        LastContinueThreadId = threadId;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> PauseAsync(int threadId, CancellationToken ct = default)
    {
        LastPauseThreadId = threadId;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> NextAsync(int threadId, CancellationToken ct = default)
    {
        LastNextThreadId = threadId;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> StepInAsync(int threadId, CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapResponse> StepOutAsync(int threadId, CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapResponse> StackTraceAsync(int threadId, CancellationToken ct = default)
    {
        LastStackTraceThreadId = threadId;
        return Task.FromResult(StackTraceResponse ?? DefaultResponse);
    }

    public Task<DapResponse> ThreadsAsync(CancellationToken ct = default)
        => Task.FromResult(ThreadsResponse ?? DefaultResponse);

    public Task<DapResponse> ScopesAsync(int frameId, CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapResponse> VariablesAsync(int variablesReference, CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapResponse> EvaluateAsync(string expression, int frameId, CancellationToken ct = default)
        => Task.FromResult(DefaultResponse);

    public Task<DapEvent?> WaitForEventAsync(string eventName, TimeSpan timeout, CancellationToken ct = default)
        => Task.FromResult<DapEvent?>(null);
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build ndb.slnx`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add tests/Ndb.Tests/FakeDapClient.cs
git commit -m "test: add FakeDapClient test double"
```

---

## Task 4: Add RequestDispatcher unit tests

**Files:**
- Create: `tests/Ndb.Tests/RequestDispatcherTests.cs`

Note: `RequestDispatcher` requires a `FileLogger`. Create a temp file logger for tests. The `ResolveThreadIdAsync` method is private, so we test it indirectly through `DispatchAsync` with `exec.pause` (which calls ResolveThreadIdAsync and records `LastPauseThreadId` on the fake). For stopped events and continue, we use `exec.continue` with `wait=false`.

- [ ] **Step 1: Create test file with helper method and first test**

Create `tests/Ndb.Tests/RequestDispatcherTests.cs`:

```csharp
using System.Text.Json;
using Ndb.Daemon;
using Ndb.Dap;
using Ndb.Dap.Messages;
using Ndb.Ipc;
using Ndb.Json;

namespace Ndb.Tests;

public class RequestDispatcherTests : IDisposable
{
    private readonly FakeDapClient _dap = new();
    private readonly FileLogger _logger;
    private readonly string _logPath;

    public RequestDispatcherTests()
    {
        _logPath = Path.GetTempFileName();
        _logger = new FileLogger(_logPath, verbose: true);
    }

    public void Dispose()
    {
        _logger.Dispose();
        try { File.Delete(_logPath); } catch { }
    }

    private RequestDispatcher CreateDispatcher() => new(_dap, _logger);

    private static IpcRequest MakeRequest(string method, JsonElement? @params = null)
        => new() { Id = 1, Method = method, Params = @params };

    private static JsonElement Json(object obj)
        => JsonSerializer.SerializeToElement(obj, NdbJsonContext.Default.JsonElement);

    // --- Thread Resolution Tests ---

    [Fact]
    public async Task Pause_WithExplicitThread_UsesExplicitThread()
    {
        // Initialize session first
        var dispatcher = CreateDispatcher();
        await dispatcher.DispatchAsync(MakeRequest("launch", Json(new { program = "test.dll" })), CancellationToken.None);

        await dispatcher.DispatchAsync(
            MakeRequest("exec.pause", Json(new { threadId = 5 })),
            CancellationToken.None);

        Assert.Equal(5, _dap.LastPauseThreadId);
    }
}
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test tests/Ndb.Tests --filter "Pause_WithExplicitThread_UsesExplicitThread" -v n`
Expected: 1 passed

- [ ] **Step 3: Add remaining thread resolution tests**

Append to the `RequestDispatcherTests` class:

```csharp
    [Fact]
    public async Task Pause_NoThread_UsesLastStoppedThread()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.DispatchAsync(MakeRequest("launch", Json(new { program = "test.dll" })), CancellationToken.None);

        // Simulate a stopped event by dispatching continue --wait with a fake stopped event
        // Instead, we use step-over which calls ResolveThreadIdAsync
        // First, set _lastStoppedThreadId by processing a stopped result through HandleExecAsync
        // Simpler: use exec.pause twice — first with explicit thread to establish state, then without
        // Actually, _lastStoppedThreadId is set in BuildStoppedResultAsync, not by pause.
        // We need to test through the full flow. Let's set up a WaitForEvent that returns a stopped event.

        // Configure fake to return a stopped event for continue --wait
        var stoppedBody = JsonSerializer.SerializeToElement(
            new { reason = "breakpoint", threadId = 42 }, NdbJsonContext.Default.JsonElement);
        var stoppedEvent = new DapEvent { Event = "stopped", Body = stoppedBody };

        // Override WaitForEventAsync to return our event
        _dap.NextStoppedEvent = stoppedEvent;

        // continue --wait triggers BuildStoppedResultAsync which sets _lastStoppedThreadId
        await dispatcher.DispatchAsync(
            MakeRequest("exec.continue", Json(new { wait = true, timeout = 5 })),
            CancellationToken.None);

        // Now pause without explicit thread should use lastStoppedThreadId=42
        await dispatcher.DispatchAsync(
            MakeRequest("exec.pause"),
            CancellationToken.None);

        Assert.Equal(42, _dap.LastPauseThreadId);
    }
```

Wait — this requires `FakeDapClient` to support a configurable `WaitForEventAsync`. Let me update the plan. We need to add `NextStoppedEvent` to `FakeDapClient`.

- [ ] **Step 4: Update FakeDapClient to support configurable WaitForEventAsync**

In `tests/Ndb.Tests/FakeDapClient.cs`, change the `WaitForEventAsync` method and add a field:

```csharp
    public DapEvent? NextStoppedEvent { get; set; }

    public Task<DapEvent?> WaitForEventAsync(string eventName, TimeSpan timeout, CancellationToken ct = default)
    {
        var evt = NextStoppedEvent;
        NextStoppedEvent = null; // consume once
        return Task.FromResult(evt);
    }
```

- [ ] **Step 5: Add ThreadsResponse helper for fallback test**

We need FakeDapClient to return threads for the fallback test. `ThreadsResponse` is already defined. We need to build a proper DapResponse with a body containing threads JSON.

Add a static helper to the test class:

```csharp
    private static DapResponse MakeThreadsResponse(params int[] threadIds)
    {
        var threads = threadIds.Select(id => new { id, name = $"Thread {id}" }).ToArray();
        var body = JsonSerializer.SerializeToElement(new { threads }, NdbJsonContext.Default.JsonElement);
        return new DapResponse { Success = true, Body = body };
    }
```

- [ ] **Step 6: Add all remaining tests**

Append these tests to `RequestDispatcherTests`:

```csharp
    [Fact]
    public async Task Pause_NoThread_FallsBackToFirstThread()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.DispatchAsync(MakeRequest("launch", Json(new { program = "test.dll" })), CancellationToken.None);

        _dap.ThreadsResponse = MakeThreadsResponse(100, 200);

        await dispatcher.DispatchAsync(
            MakeRequest("exec.pause"),
            CancellationToken.None);

        Assert.Equal(100, _dap.LastPauseThreadId);
    }

    [Fact]
    public async Task Pause_NoThreads_SendsZero()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.DispatchAsync(MakeRequest("launch", Json(new { program = "test.dll" })), CancellationToken.None);

        _dap.ThreadsResponse = new DapResponse { Success = true };

        await dispatcher.DispatchAsync(
            MakeRequest("exec.pause"),
            CancellationToken.None);

        Assert.Equal(0, _dap.LastPauseThreadId);
    }

    [Fact]
    public async Task Continue_ResetsLastStoppedThread()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.DispatchAsync(MakeRequest("launch", Json(new { program = "test.dll" })), CancellationToken.None);

        // Set lastStoppedThreadId via continue --wait with stopped event
        var stoppedBody = JsonSerializer.SerializeToElement(
            new { reason = "breakpoint", threadId = 42 }, NdbJsonContext.Default.JsonElement);
        _dap.NextStoppedEvent = new DapEvent { Event = "stopped", Body = stoppedBody };

        await dispatcher.DispatchAsync(
            MakeRequest("exec.continue", Json(new { wait = true, timeout = 5 })),
            CancellationToken.None);

        // Now continue without wait — should reset _lastStoppedThreadId
        await dispatcher.DispatchAsync(
            MakeRequest("exec.continue", Json(new { wait = false })),
            CancellationToken.None);

        // Pause should now fall back to ThreadsAsync
        _dap.ThreadsResponse = MakeThreadsResponse(99);
        await dispatcher.DispatchAsync(MakeRequest("exec.pause"), CancellationToken.None);

        Assert.Equal(99, _dap.LastPauseThreadId);
    }

    // --- Exception Filter Tests ---

    [Fact]
    public async Task ExceptionFilter_None_ClearsFilters()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.DispatchAsync(MakeRequest("launch", Json(new { program = "test.dll" })), CancellationToken.None);

        // First set a filter
        await dispatcher.DispatchAsync(
            MakeRequest("breakpoint.exception", Json(new { filter = "all" })),
            CancellationToken.None);

        Assert.Equal(new[] { "all" }, _dap.LastExceptionFilters);

        // Clear with "none"
        await dispatcher.DispatchAsync(
            MakeRequest("breakpoint.exception", Json(new { filter = "none" })),
            CancellationToken.None);

        Assert.Empty(_dap.LastExceptionFilters!);
    }

    // --- Stop Behavior Tests ---

    [Fact]
    public async Task Stop_AfterLaunch_Terminates()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.DispatchAsync(MakeRequest("launch", Json(new { program = "test.dll" })), CancellationToken.None);

        await dispatcher.DispatchAsync(MakeRequest("stop"), CancellationToken.None);

        Assert.True(_dap.LastDisconnectTerminate);
    }

    [Fact]
    public async Task Stop_AfterAttach_Detaches()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.DispatchAsync(MakeRequest("attach", Json(new { processId = 1234 })), CancellationToken.None);

        await dispatcher.DispatchAsync(MakeRequest("stop"), CancellationToken.None);

        Assert.False(_dap.LastDisconnectTerminate);
    }

    [Fact]
    public async Task StoppedEvent_UpdatesLastStoppedThread()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.DispatchAsync(MakeRequest("launch", Json(new { program = "test.dll" })), CancellationToken.None);

        var stoppedBody = JsonSerializer.SerializeToElement(
            new { reason = "step", threadId = 7 }, NdbJsonContext.Default.JsonElement);
        _dap.NextStoppedEvent = new DapEvent { Event = "stopped", Body = stoppedBody };

        await dispatcher.DispatchAsync(
            MakeRequest("exec.continue", Json(new { wait = true, timeout = 5 })),
            CancellationToken.None);

        // Pause should use lastStoppedThreadId=7
        await dispatcher.DispatchAsync(MakeRequest("exec.pause"), CancellationToken.None);

        Assert.Equal(7, _dap.LastPauseThreadId);
    }
```

- [ ] **Step 7: Run all tests**

Run: `dotnet test ndb.slnx --filter "FullyQualifiedName!~IntegrationTests"`
Expected: 49/49 pass (40 existing + 9 new), 0 failures

- [ ] **Step 8: Commit**

```bash
git add tests/Ndb.Tests/RequestDispatcherTests.cs tests/Ndb.Tests/FakeDapClient.cs
git commit -m "test: add 9 unit tests for RequestDispatcher via IDapClient"
```

---

## Task 5: Push and verify

- [ ] **Step 1: Push all commits**

```bash
git push
```

- [ ] **Step 2: Final verification**

Run: `dotnet test ndb.slnx --filter "FullyQualifiedName!~IntegrationTests"`
Expected: 49/49 pass, 0 failures
