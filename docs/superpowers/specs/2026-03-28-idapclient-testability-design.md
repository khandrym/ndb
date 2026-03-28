# IDapClient Interface & RequestDispatcher Tests — Design Spec

## Summary

Extract `IDapClient` interface from `DapClient` to enable unit testing of `RequestDispatcher` logic without a real netcoredbg process.

## Interface

`src/Ndb.Dap/IDapClient.cs` — all 18 public async methods + `Events` property from `DapClient`.

`DapClient` implements `IDapClient`.

## Consumer Changes

- `RequestDispatcher`: field type `DapClient` → `IDapClient`
- `DaemonHost`: passes `IDapClient` to `RequestDispatcher` constructor (no other changes)

## Test Plan

`tests/Ndb.Tests/RequestDispatcherTests.cs` with `FakeDapClient : IDapClient`:

### Tests to write

1. **ResolveThreadId_ExplicitThread_ReturnsExplicit** — explicit threadId=5 → returns 5
2. **ResolveThreadId_LastStopped_ReturnsLastStopped** — threadId=0, lastStopped=3 → returns 3
3. **ResolveThreadId_FallbackToFirstThread** — threadId=0, no lastStopped → returns first from ThreadsAsync
4. **ResolveThreadId_NoThreads_ReturnsZero** — threadId=0, no lastStopped, empty threads → returns 0
5. **StoppedEvent_UpdatesLastStoppedThreadId** — stopped event with threadId=7 → next resolve returns 7
6. **Continue_ResetsLastStoppedThreadId** — after continue, lastStopped resets to 0
7. **ExceptionFilter_None_ClearsFilters** — filter="none" → calls SetExceptionBreakpoints with empty array
8. **Stop_AfterLaunch_Terminates** — stop after launch → DisconnectAsync(terminateDebuggee: true)
9. **Stop_AfterAttach_Detaches** — stop after attach → DisconnectAsync(terminateDebuggee: false)

### FakeDapClient

Simple test double in the test project. No mocking framework. Returns configurable `DapResponse` and `DapEvent` objects. Tracks calls for assertion (e.g., `LastDisconnectTerminate` to verify stop behavior).

## Files to Change

| File | Change |
|---|---|
| `src/Ndb.Dap/IDapClient.cs` | New: interface with all 18 methods + Events |
| `src/Ndb.Dap/DapClient.cs` | Add `: IDapClient` |
| `src/Ndb/Daemon/RequestDispatcher.cs` | `DapClient` → `IDapClient` |
| `src/Ndb/Daemon/DaemonHost.cs` | No type change needed (already passes as constructor arg) |
| `tests/Ndb.Tests/FakeDapClient.cs` | New: test double |
| `tests/Ndb.Tests/RequestDispatcherTests.cs` | New: 9 unit tests |

## Non-Goals

- Mocking framework (Moq, NSubstitute) — FakeDapClient is sufficient
- Testing DaemonHost (integration test territory)
- Testing DapClient itself (already has tests in Ndb.Dap.Tests)
