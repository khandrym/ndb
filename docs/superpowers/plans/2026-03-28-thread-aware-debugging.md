# Thread-Aware Debugging & Stability Fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix five critical issues discovered during real-world multi-threaded debugging: wrong thread resolution, step-over failures, exception filter clearing, daemon blocking during `--wait`, and process crash on detach.

**Architecture:** Add `_lastStoppedThreadId` tracking to `RequestDispatcher` as the central fix for thread resolution. Add `--thread` to inspection commands. Make daemon accept loop concurrent. Fix exception filter edge case. Add graceful shutdown delay.

**Tech Stack:** C# / .NET 10, System.CommandLine, System.Text.Json source generators, xUnit

---

## File Structure

### New files

```
(none)
```

### Modified files

```
src/Ndb/Daemon/RequestDispatcher.cs     # _lastStoppedThreadId, thread resolution, exception filter fix
src/Ndb/Cli/InspectCommands.cs          # --thread on variables and evaluate
src/Ndb/Models/InspectData.cs           # ThreadId in InspectVariablesParams and InspectEvaluateParams
src/Ndb/Daemon/DaemonHost.cs            # Concurrent request handling, graceful shutdown
```

---

## Task 1: Last Stopped Thread Tracking

**Files:**
- Modify: `src/Ndb/Daemon/RequestDispatcher.cs`

- [ ] **Step 1: Add `_lastStoppedThreadId` field**

In `RequestDispatcher`, add field after `_isAttached`:

```csharp
    private int _lastStoppedThreadId;
```

- [ ] **Step 2: Create `ResolveThreadIdAsync` helper method**

Add a private method that encapsulates the thread resolution logic used by all handlers:

```csharp
    private async Task<int> ResolveThreadIdAsync(int requestedThreadId, CancellationToken ct)
    {
        if (requestedThreadId != 0)
            return requestedThreadId;

        if (_lastStoppedThreadId != 0)
            return _lastStoppedThreadId;

        var threadsResp = await _dap.ThreadsAsync(ct);
        if (threadsResp.Success && threadsResp.Body.HasValue)
        {
            var threads = threadsResp.Body.Value.Deserialize(DapJsonContext.Default.ThreadsResponseBody);
            if (threads?.Threads.Length > 0)
                return threads.Threads[0].Id;
        }

        return 0;
    }
```

- [ ] **Step 3: Update `BuildStoppedResultAsync` to track stopped thread**

At the top of `BuildStoppedResultAsync`, after extracting `threadId` from the event body, add:

```csharp
        if (threadId.HasValue)
            _lastStoppedThreadId = threadId.Value;
```

- [ ] **Step 4: Update `HandleExecAsync` to use `ResolveThreadIdAsync`**

Replace the inline thread resolution block (lines 397-407):

```csharp
        // Step commands require a valid thread ID — resolve first thread if not specified
        if (threadId == 0 && dapCommand != "continue")
        {
            var threadsResp = await _dap.ThreadsAsync(ct);
            ...
        }
```

With:

```csharp
        if (threadId == 0 && dapCommand != "continue")
            threadId = await ResolveThreadIdAsync(threadId, ct);
```

- [ ] **Step 5: Update `HandleExecPauseAsync` to use `ResolveThreadIdAsync`**

Replace the inline thread resolution block (lines 457-466) with:

```csharp
        if (threadId == 0)
            threadId = await ResolveThreadIdAsync(threadId, ct);
```

- [ ] **Step 6: Update `HandleRunToCursorAsync` to use `ResolveThreadIdAsync`**

Replace the inline thread resolution block (lines 489-496) with:

```csharp
        var threadId = await ResolveThreadIdAsync(0, ct);
```

- [ ] **Step 7: Update `HandleInspectStacktraceAsync` to use `ResolveThreadIdAsync`**

Replace the inline thread resolution block (lines 581-590) with:

```csharp
        if (threadId == 0)
            threadId = await ResolveThreadIdAsync(threadId, ct);
```

- [ ] **Step 8: Update `HandleInspectVariablesAsync` to use `ResolveThreadIdAsync`**

In the standard mode block (lines 648-662), replace:

```csharp
        if (!frameId.HasValue)
        {
            var threadsResp = await _dap.ThreadsAsync(ct);
            var threads = threadsResp.Body?.Deserialize(DapJsonContext.Default.ThreadsResponseBody);
            var firstThread = threads?.Threads.FirstOrDefault();
            if (firstThread is null)
                return IpcResponse.Err(request.Id, -1, "no threads available");

            var stResp = await _dap.StackTraceAsync(firstThread.Id, ct);
            ...
        }
```

With:

```csharp
        // Read threadId from params (new)
        int requestedThreadId = 0;
        if (request.Params.HasValue && request.Params.Value.TryGetProperty("threadId", out var tidProp))
            requestedThreadId = tidProp.GetInt32();

        if (!frameId.HasValue)
        {
            var resolvedThreadId = await ResolveThreadIdAsync(requestedThreadId, ct);
            if (resolvedThreadId == 0)
                return IpcResponse.Err(request.Id, -1, "no threads available");

            var stResp = await _dap.StackTraceAsync(resolvedThreadId, ct);
            var st = stResp.Body?.Deserialize(DapJsonContext.Default.StackTraceResponseBody);
            var topFrame = st?.StackFrames.FirstOrDefault();
            if (topFrame is null)
                return IpcResponse.Err(request.Id, -1, "no frames available");
            frameId = topFrame.Id;
        }
```

- [ ] **Step 9: Update `HandleInspectEvaluateAsync` to use `ResolveThreadIdAsync`**

Replace the thread/frame resolution block (lines 707-718):

```csharp
        if (frameId == 0)
        {
            var threadsResp = await _dap.ThreadsAsync(ct);
            ...
        }
```

With:

```csharp
        // Read threadId from params (new)
        int requestedThreadId = 0;
        if (p.TryGetProperty("threadId", out var tidProp))
            requestedThreadId = tidProp.GetInt32();

        if (frameId == 0)
        {
            var resolvedThreadId = await ResolveThreadIdAsync(requestedThreadId, ct);
            if (resolvedThreadId != 0)
            {
                var stResp = await _dap.StackTraceAsync(resolvedThreadId, ct);
                var st = stResp.Body?.Deserialize(DapJsonContext.Default.StackTraceResponseBody);
                var topFrame = st?.StackFrames.FirstOrDefault();
                if (topFrame is not null) frameId = topFrame.Id;
            }
        }
```

- [ ] **Step 10: Reset `_lastStoppedThreadId` on continue**

In `HandleExecAsync`, after the `continue` DAP command succeeds and before the wait logic, reset the tracked thread:

```csharp
        if (dapCommand == "continue")
            _lastStoppedThreadId = 0;
```

- [ ] **Step 11: Verify build**

```bash
dotnet build ndb.slnx -v q
```

Expected: 0 errors

- [ ] **Step 12: Run existing tests**

```bash
dotnet test tests/Ndb.Tests tests/Ndb.Dap.Tests -v q
```

Expected: all pass

- [ ] **Step 13: Commit**

```bash
git add src/Ndb/Daemon/RequestDispatcher.cs
git commit -m "feat: track last stopped thread for correct multi-threaded debugging

- Add _lastStoppedThreadId field, updated on every stopped event
- Add ResolveThreadIdAsync helper: explicit thread > last stopped > first thread
- All exec/inspect handlers use ResolveThreadIdAsync instead of inline resolution
- Reset tracked thread on continue
- Fixes step-over failing with 0x80004005 on wrong thread"
```

---

## Task 2: Add `--thread` to `inspect variables` and `inspect evaluate`

**Files:**
- Modify: `src/Ndb/Cli/InspectCommands.cs`
- Modify: `src/Ndb/Models/InspectData.cs`

- [ ] **Step 1: Add `ThreadId` to `InspectVariablesParams`**

In `src/Ndb/Models/InspectData.cs`, add to `InspectVariablesParams`:

```csharp
    [JsonPropertyName("threadId")]
    public int ThreadId { get; init; }
```

- [ ] **Step 2: Add `ThreadId` to `InspectEvaluateParams`**

In `src/Ndb/Models/InspectData.cs`, add to `InspectEvaluateParams`:

```csharp
    [JsonPropertyName("threadId")]
    public int ThreadId { get; init; }
```

- [ ] **Step 3: Add `--thread` option to `CreateVariables()`**

In `src/Ndb/Cli/InspectCommands.cs`, in `CreateVariables()`, add after the existing options:

```csharp
        var threadOption = new Option<int>("--thread") { Description = "Thread ID (0 = last stopped thread)" };
```

Add to command:

```csharp
        cmd.Add(threadOption);
```

In the action, read `threadId` and pass in params for the non-expand case:

```csharp
                var threadId = pr.GetValue(threadOption);
                var p = new InspectVariablesParams { FrameId = frameId, ScopeIndex = scopeIndex, ThreadId = threadId };
```

- [ ] **Step 4: Add `--thread` option to `CreateEvaluate()`**

In `src/Ndb/Cli/InspectCommands.cs`, in `CreateEvaluate()`, add:

```csharp
        var threadOption = new Option<int>("--thread") { Description = "Thread ID (0 = last stopped thread)" };
        cmd.Add(threadOption);
```

In the action:

```csharp
            var threadId = pr.GetValue(threadOption);
            var p = new InspectEvaluateParams { Expression = expression, FrameId = frameId, ThreadId = threadId };
```

- [ ] **Step 5: Verify build**

```bash
dotnet build ndb.slnx -v q
```

Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add src/Ndb/Cli/InspectCommands.cs src/Ndb/Models/InspectData.cs
git commit -m "feat: add --thread option to inspect variables and inspect evaluate"
```

---

## Task 3: Fix `breakpoint exception --filter none`

**Files:**
- Modify: `src/Ndb/Daemon/RequestDispatcher.cs`

- [ ] **Step 1: Handle "none" as clear in `HandleBreakpointExceptionAsync`**

In `RequestDispatcher.HandleBreakpointExceptionAsync`, replace:

```csharp
        if (clear)
        {
            _exceptionFilters = [];
        }
        else if (filter is not null)
        {
            if (!_exceptionFilters.Contains(filter))
                _exceptionFilters = [.. _exceptionFilters, filter];
        }
```

With:

```csharp
        if (clear || string.Equals(filter, "none", StringComparison.OrdinalIgnoreCase))
        {
            _exceptionFilters = [];
        }
        else if (filter is not null)
        {
            if (!_exceptionFilters.Contains(filter))
                _exceptionFilters = [.. _exceptionFilters, filter];
        }
```

- [ ] **Step 2: Verify build**

```bash
dotnet build ndb.slnx -v q
```

- [ ] **Step 3: Commit**

```bash
git add src/Ndb/Daemon/RequestDispatcher.cs
git commit -m "fix: handle 'none' as clear in breakpoint exception filter"
```

---

## Task 4: Non-blocking daemon request handling

**Files:**
- Modify: `src/Ndb/Daemon/DaemonHost.cs`

- [ ] **Step 1: Make request handling concurrent**

In `DaemonHost.RunAsync`, replace the sequential accept loop (lines 102-126):

```csharp
            while (!ct.IsCancellationRequested)
            {
                logger.Debug("Waiting for next connection...");
                Stream clientStream;
                try { clientStream = await server.AcceptAsync(ct); }
                catch (OperationCanceledException) { break; }

                try
                {
                    var request = await ReadRequestAsync(clientStream, ct);
                    if (request is null) { clientStream.Dispose(); continue; }

                    logger.Debug($"Received: {request.Method}");
                    var resp = await dispatcher.DispatchAsync(request, ct);
                    await SendResponseAsync(clientStream, resp);

                    if (request.Method == "stop")
                    {
                        logger.Info("Stop requested, shutting down");
                        clientStream.Dispose();
                        break;
                    }
                }
                finally { clientStream.Dispose(); }
            }
```

With:

```csharp
            var stopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            while (!stopCts.Token.IsCancellationRequested)
            {
                logger.Debug("Waiting for next connection...");
                Stream clientStream;
                try { clientStream = await server.AcceptAsync(stopCts.Token); }
                catch (OperationCanceledException) { break; }

                // Handle each request concurrently so --wait doesn't block new connections
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var request = await ReadRequestAsync(clientStream, ct);
                        if (request is null) { clientStream.Dispose(); return; }

                        logger.Debug($"Received: {request.Method}");
                        var resp = await dispatcher.DispatchAsync(request, ct);
                        await SendResponseAsync(clientStream, resp);

                        if (request.Method == "stop")
                        {
                            logger.Info("Stop requested, shutting down");
                            stopCts.Cancel();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Request handling error: {ex.Message}");
                    }
                    finally { clientStream.Dispose(); }
                }, stopCts.Token);
            }
```

- [ ] **Step 2: Verify build**

```bash
dotnet build ndb.slnx -v q
```

- [ ] **Step 3: Commit**

```bash
git add src/Ndb/Daemon/DaemonHost.cs
git commit -m "feat: handle daemon requests concurrently to prevent --wait blocking"
```

---

## Task 5: Graceful netcoredbg shutdown after detach

**Files:**
- Modify: `src/Ndb/Daemon/DaemonHost.cs`

- [ ] **Step 1: Add graceful wait before killing netcoredbg**

In `DaemonHost.RunAsync`, replace (lines 128-131):

```csharp
            if (!process.HasExited)
            {
                try { process.Kill(); } catch { }
            }
```

With:

```csharp
            if (!process.HasExited)
            {
                try
                {
                    // Give netcoredbg time to detach cleanly
                    if (!process.WaitForExit(5000))
                    {
                        logger.Info("netcoredbg did not exit in 5s, killing");
                        process.Kill();
                    }
                }
                catch { }
            }
```

- [ ] **Step 2: Verify build**

```bash
dotnet build ndb.slnx -v q
```

- [ ] **Step 3: Run all tests**

```bash
dotnet test tests/Ndb.Tests tests/Ndb.Dap.Tests -v q
```

Expected: all pass

- [ ] **Step 4: Commit**

```bash
git add src/Ndb/Daemon/DaemonHost.cs
git commit -m "fix: wait for netcoredbg to exit gracefully after detach before killing"
```

---

## Task 6: Integration test and final verification

- [ ] **Step 1: Run full test suite**

```bash
dotnet test ndb.slnx --filter "FullyQualifiedName!~IntegrationTests" -v q
```

Expected: all pass

- [ ] **Step 2: Run integration tests**

```bash
NETCOREDBG_PATH="D:/ANDRII/WORK/PROGRAMMING/03_AI/ndb/src/Ndb/bin/Debug/net10.0/netcoredbg/netcoredbg/netcoredbg.exe" dotnet test tests/Ndb.IntegrationTests -v n --timeout 120000
```

Expected: all pass

- [ ] **Step 3: Smoke test — multi-threaded breakpoint scenario**

Build and run manually:

```bash
dotnet build ndb.slnx -v q
dotnet run --project src/Ndb -- version
```

Verify `--thread` appears in help:

```bash
dotnet run --project src/Ndb -- inspect variables --help
dotnet run --project src/Ndb -- inspect evaluate --help
```

Expected: `--thread` option listed for both commands.

Verify exception filter clear:

```bash
# (requires active debug session)
# ndb breakpoint exception --filter all
# ndb breakpoint exception --filter none   <-- should succeed now
```

- [ ] **Step 4: Push**

```bash
git push
```

---

## Summary

| Task | What it fixes |
|---|---|
| 1 | `_lastStoppedThreadId` tracking — step-over and inspect use correct thread |
| 2 | `--thread` on `inspect variables` and `inspect evaluate` — explicit thread targeting |
| 3 | `breakpoint exception --filter none` — clears exception filters instead of error |
| 4 | Concurrent daemon request handling — `--wait` no longer blocks other commands |
| 5 | Graceful netcoredbg shutdown — `ndb stop` after `attach` won't crash target process |
| 6 | Full verification — all tests pass, smoke test |

**After these fixes:**
```bash
# Breakpoint hits on worker thread 32328
ndb inspect stacktrace                    # shows thread 32328 stack (not first thread)
ndb inspect variables                     # shows thread 32328 variables (not first thread)
ndb inspect variables --thread 32328      # explicit thread targeting
ndb exec step-over                        # steps on thread 32328 (not error)
ndb exec continue --wait &               # doesn't block other commands
ndb inspect threads                       # works while continue --wait is running
ndb breakpoint exception --filter none    # clears exception filters
ndb stop                                  # detaches cleanly without killing target
```
