# ndb Phase 3 — Polish Design

## Goal

Polish the ndb CLI with exception breakpoints, nested variable expansion, conditional breakpoints verification, improved error messages, Unix Domain Sockets for cross-platform support, and CI/CD pipeline.

## 1. Exception Breakpoints

### CLI

```
ndb breakpoint exception --filter <filter>    # set exception filter
ndb breakpoint exception --clear              # clear all exception filters
```

Filters supported by netcoredbg: `all` (all exceptions), `user-unhandled` (unhandled in user code).

### Implementation

- DAP: `setExceptionBreakpoints` request with `{filters: ["all"]}` argument
- New message: `SetExceptionBreakpointsArguments` — `{filters: string[]}`
- DapClient: `SetExceptionBreakpointsAsync(string[] filters)`
- RequestDispatcher: `breakpoint.exception` handler
- Daemon stores current exception filters in `string[]` field on RequestDispatcher

## 2. Nested Variables (expand by variablesReference)

### CLI

```
ndb inspect variables                    # top-level (existing)
ndb inspect variables --expand <ref>     # expand a variablesReference
```

### Implementation

- Add `--expand <ref>` option to `inspect variables` command
- If `--expand` is provided, call `variables` DAP directly with that reference
- Add `expandable` (bool) and `ref` (int) fields to `VarResult` model
- `expandable = variablesReference > 0` from DAP response

## 3. Conditional Breakpoints (Verification)

The `--condition` flag already exists in CLI and `BreakpointManager` stores conditions. The `SourceBreakpoint` type has a `Condition` field.

Task: verify end-to-end that conditions propagate through the full chain and write an integration test.

## 4. Improved Error Messages

1. **Status with exitCode** — `ndb status` shows `exitCode` when session terminated
2. **netcoredbg crash** — include log path in error: `"debuggee exited with code 1. Log: /path/to/log"`
3. **Breakpoint message** — propagate DAP `message` field from breakpoint verification response
4. **DAP error details** — ensure all DAP error messages are forwarded to CLI

## 5. Unix Domain Sockets

### New Transports

- `UnixSocketServerTransport : IServerTransport` — listens on `/tmp/ndb-<pid>.sock`
- `UnixSocketClientTransport : ITransport` — connects to Unix socket

Both use `System.Net.Sockets.Socket` with `UnixDomainSocketEndPoint`.

### Platform Selection

- `DaemonHost` and CLI commands select transport based on `OperatingSystem.IsWindows()`
- Windows → Named Pipes (existing)
- Linux/macOS → Unix Domain Sockets (new)
- `SessionInfo` already stores pipe/socket name — works for both

### Testing

Tests marked with `[Trait("Platform", "Unix")]` — run via WSL or Linux CI.

## 6. CI/CD

### ci.yml — Continuous Integration

- Trigger: push/PR to main
- Matrix: Windows (unit + integration tests)
- Steps: checkout, setup .NET 10, restore, build, download netcoredbg, test

### release.yml — Release

- Trigger: tag `v*`
- Matrix: win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64
- Steps: checkout, setup .NET 10, publish Native AOT, package as `ndb-<platform>.zip`, upload to GitHub Release
- Single binary only (no bundled variant — `ndb setup` downloads netcoredbg)
