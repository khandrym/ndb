# Thread-Aware Debugging & Stability Fixes — Design Spec

## Summary

Fix critical issues discovered during real-world multi-threaded debugging of AxSysSim (WPF/.NET application with ThreadPool workers). Five problems, one root cause: ndb has no concept of "current thread" and defaults to the first thread in the list, which is rarely the thread that hit the breakpoint.

## Problems

### 1. No `--thread` on `inspect variables` and `inspect evaluate`

`inspect stacktrace` has `--thread`, but `variables` and `evaluate` do not. When a breakpoint fires on a worker thread, there is no way to inspect variables on that thread — ndb always resolves to the first thread (usually an idle IO thread).

### 2. `step-over` fails with `0x80004005` after breakpoint hit

When stopped on a breakpoint, `exec step-over` auto-resolves to `threads.Threads[0]` — a thread that is NOT stopped. netcoredbg rejects the `next` command because that thread is running. The actual stopped thread is buried in the thread list.

### 3. `breakpoint exception --filter none` fails

The handler treats `"none"` as a literal filter name and tries to add it to the exception filters array. netcoredbg does not recognize `"none"` and returns an error. There is no way to clear exception filters once set.

### 4. Daemon blocks during `--wait` commands

`DaemonHost` processes requests sequentially: `AcceptAsync → Dispatch → SendResponse → AcceptAsync`. When a `continue --wait` is dispatched, the daemon blocks on `WaitForEventAsync` and cannot accept new connections. The CLI times out with "daemon not responding".

### 5. `ndb stop` after `attach` kills netcoredbg abruptly

After `DisconnectAsync(terminateDebuggee: false)`, `DaemonHost` immediately checks `!process.HasExited` and calls `process.Kill()`. netcoredbg may not have finished detaching yet, which can leave the target process in a bad state or crash it.

## Design

### Last Stopped Thread Tracking (fixes #1, #2)

Add `_lastStoppedThreadId` field to `RequestDispatcher`. Update it whenever a `stopped` event is received (from breakpoint, step, pause, or exception). Use it as the default thread for all commands that need a thread:

- `inspect stacktrace` (when `--thread` not specified)
- `inspect variables` (when `--thread` not specified)
- `inspect evaluate` (when `--thread` not specified)
- `exec step-over/step-into/step-out` (when `--thread` not specified)

**Resolution order:** explicit `--thread` > `_lastStoppedThreadId` > first thread from list.

This single change fixes both the "wrong thread" inspection and the "step-over on running thread" problems.

### CLI: Add `--thread` to `variables` and `evaluate`

Add `--thread` option to both commands in `InspectCommands.cs`. Pass `threadId` to dispatcher via IPC params. In `RequestDispatcher`, use `threadId` to resolve the correct stack frame for scopes/variables/evaluate.

### Model Changes

Add `threadId` to `InspectVariablesParams` and `InspectEvaluateParams`.

### Fix `breakpoint exception --filter none` (fix #3)

In `HandleBreakpointExceptionAsync`, treat `"none"` as a clear command:

```csharp
if (clear || string.Equals(filter, "none", StringComparison.OrdinalIgnoreCase))
    _exceptionFilters = [];
```

### Non-blocking `--wait` (fix #4)

Move `--wait` handling out of the request dispatch pipeline. When `wait=true`, start the event wait on a background task and respond immediately. Add a new IPC method `wait` that blocks until the background wait completes and returns the stopped result.

**New command:** `ndb wait [--timeout <sec>] [--session <name>]`

Alternatively, simpler approach: run each incoming request on its own `Task` so the accept loop is never blocked. The daemon can handle multiple concurrent requests. Since DAP is sequential, the dispatcher uses a SemaphoreSlim to serialize DAP calls, but the IPC accept loop remains free.

**Chosen approach:** concurrent request handling. Simpler, no new commands, no client-side changes. The `--wait` mechanism works as before from the user's perspective.

### Graceful netcoredbg shutdown after detach (fix #5)

After `DisconnectAsync`, wait for netcoredbg to exit gracefully (up to 5 seconds) before killing it:

```csharp
if (!process.HasExited)
{
    if (!process.WaitForExit(5000))
        try { process.Kill(); } catch { }
}
```

## Files to Change

| File | Change |
|---|---|
| `src/Ndb/Daemon/RequestDispatcher.cs` | Add `_lastStoppedThreadId`, update thread resolution in all handlers, fix exception filter |
| `src/Ndb/Cli/InspectCommands.cs` | Add `--thread` to `variables` and `evaluate` |
| `src/Ndb/Models/InspectData.cs` | Add `ThreadId` to `InspectVariablesParams` and `InspectEvaluateParams` |
| `src/Ndb/Json/NdbJsonContext.cs` | No changes needed (existing types updated) |
| `src/Ndb/Daemon/DaemonHost.cs` | Concurrent request handling, graceful netcoredbg shutdown |
| `tests/Ndb.Tests/RequestDispatcherTests.cs` | Tests for thread resolution logic |

## Non-Goals

- Thread switching/freezing (out of scope for this release)
- Expression evaluation improvements (netcoredbg limitation)
- DAP-level timeout handling
