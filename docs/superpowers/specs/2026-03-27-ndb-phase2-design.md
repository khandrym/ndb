# ndb Phase 2 — Core Debugging Design

## Goal

Add core debugging capabilities to ndb: attach to running processes, set/manage breakpoints, control execution (step, continue, pause), and inspect program state (stacktrace, variables, evaluate). The `--wait` flag on execution commands allows AI agents to get the full result in a single CLI call.

## New DAP Methods in DapClient

| CLI Command | DAP Request | DapClient Method |
|---|---|---|
| `attach` | `attach` | `AttachAsync(int processId)` |
| `breakpoint set/remove` | `setBreakpoints` | `SetBreakpointsAsync(string file, SourceBreakpoint[])` |
| `exec continue` | `continue` | `ContinueAsync(int threadId)` |
| `exec pause` | `pause` | `PauseAsync(int threadId)` |
| `exec step-over` | `next` | `NextAsync(int threadId)` |
| `exec step-into` | `stepIn` | `StepInAsync(int threadId)` |
| `exec step-out` | `stepOut` | `StepOutAsync(int threadId)` |
| `inspect stacktrace` | `stackTrace` | `StackTraceAsync(int threadId)` |
| `inspect threads` | `threads` | `ThreadsAsync()` |
| `inspect variables` | `scopes` + `variables` | `ScopesAsync(int frameId)` + `VariablesAsync(int variablesReference)` |
| `inspect evaluate` | `evaluate` | `EvaluateAsync(string expression, int frameId)` |
| `inspect source` | (file read) | Not DAP — daemon reads file from disk |

Additionally: `WaitForEventAsync(string eventName, TimeSpan? timeout)` — waits for a specific DAP event (used by `--wait`).

## New DAP Message Types (Ndb.Dap/Messages/)

### Request Arguments

- `AttachArguments` — `{processId: int}`
- `SetBreakpointsArguments` — `{source: {path: string}, breakpoints: [{line: int, condition?: string}]}`
- `ContinueArguments` — `{threadId: int}`
- `PauseArguments` — `{threadId: int}`
- `StepArguments` — `{threadId: int}` (shared by next/stepIn/stepOut)
- `StackTraceArguments` — `{threadId: int}`
- `ScopesArguments` — `{frameId: int}`
- `VariablesArguments` — `{variablesReference: int}`
- `EvaluateArguments` — `{expression: string, frameId: int, context: "repl"}`

### Response Bodies (typed)

- `SetBreakpointsResponseBody` — `{breakpoints: BreakpointInfo[]}`
  - `BreakpointInfo` — `{id: int, verified: bool, line: int, message?: string}`
- `StackTraceResponseBody` — `{stackFrames: StackFrameInfo[], totalFrames?: int}`
  - `StackFrameInfo` — `{id: int, name: string, source?: {path: string}, line: int, column: int}`
- `ThreadsResponseBody` — `{threads: ThreadInfo[]}`
  - `ThreadInfo` — `{id: int, name: string}`
- `ScopesResponseBody` — `{scopes: ScopeInfo[]}`
  - `ScopeInfo` — `{name: string, variablesReference: int, expensive: bool}`
- `VariablesResponseBody` — `{variables: VariableInfo[]}`
  - `VariableInfo` — `{name: string, value: string, type?: string, variablesReference: int}`
- `EvaluateResponseBody` — `{result: string, type?: string, variablesReference: int}`

## Breakpoint Management in Daemon

DAP does not support add/remove of individual breakpoints. `setBreakpoints` sends the full list for a file. The daemon maintains breakpoint state.

### BreakpointManager

```
BreakpointManager
  _breakpoints: Dictionary<string, List<ManagedBreakpoint>>  // file path -> breakpoints
  _nextId: int  // auto-incrementing ndb-internal ID

ManagedBreakpoint:
  Id: int           // ndb-assigned, stable across DAP resends
  File: string
  Line: int
  Condition: string?
  Enabled: bool
  Verified: bool    // from DAP response
```

### Operations

- **breakpoint.set(file, line, condition?)** — adds to list, sends `setBreakpoints` for file (only enabled breakpoints), returns `{id, verified, line}`
- **breakpoint.remove(file, line)** — removes from list, resends `setBreakpoints`
- **breakpoint.list** — returns all breakpoints across all files
- **breakpoint.enable(id)** — sets Enabled=true, resends `setBreakpoints`
- **breakpoint.disable(id)** — sets Enabled=false, resends `setBreakpoints` (disabled breakpoints excluded from DAP request)

ID system: ndb generates its own IDs (1, 2, 3...) because DAP adapter assigns its own IDs that may change on resend.

## --wait Implementation

Approach: daemon waits for `stopped` event, CLI waits for IPC response.

### Flow for `ndb exec continue --wait --timeout 30`

1. CLI sends: `{"method": "exec.continue", "params": {"wait": true, "timeout": 30}}`
2. Daemon sends DAP `continue`
3. Daemon receives DAP response (success) but does NOT reply to CLI yet
4. Daemon waits for DAP `stopped` event (or timeout)
5. On `stopped` event — daemon responds to CLI:
   ```json
   {"result": {"status": "stopped", "reason": "breakpoint", "threadId": 1,
    "frame": {"file": "Program.cs", "line": 42, "name": "Main"}}}
   ```
6. On timeout — daemon responds: `{"error": {"message": "timeout after 30s, debuggee still running"}}`

### Flow without --wait

1. CLI sends: `{"method": "exec.continue", "params": {}}`
2. Daemon sends DAP `continue`, receives response
3. Daemon immediately responds to CLI: `{"result": {"status": "running"}}`

Same pattern for step-over/into/out.

## CLI Commands

### New Commands

```
ndb
├── attach --pid <PID> [--verbose]
├── breakpoint
│   ├── set <file> <line>
│   ├── remove <file> <line>
│   ├── list
│   ├── enable <id>
│   └── disable <id>
├── exec
│   ├── continue [--wait] [--timeout <sec>]
│   ├── pause
│   ├── step-over [--wait] [--timeout <sec>]
│   ├── step-into [--wait] [--timeout <sec>]
│   └── step-out [--wait] [--timeout <sec>]
└── inspect
    ├── stacktrace [--thread <id>]
    ├── threads
    ├── variables [--frame <id>] [--scope <id>]
    ├── evaluate <expr> [--frame <id>]
    └── source <file> [--line <n>] [--count <n>]
```

### IPC Method Mapping

- `ndb attach` -> `"method": "attach"` (top-level, spawns daemon like launch)
- `ndb breakpoint set` -> `"method": "breakpoint.set"`
- `ndb exec continue` -> `"method": "exec.continue"`
- `ndb inspect stacktrace` -> `"method": "inspect.stacktrace"`

### New CLI Files

- `src/Ndb/Cli/AttachCommand.cs` — spawns daemon, sends attach (like LaunchCommand but with --pid)
- `src/Ndb/Cli/BreakpointCommands.cs` — breakpoint group with subcommands, all use DaemonConnector
- `src/Ndb/Cli/ExecCommands.cs` — exec group with subcommands
- `src/Ndb/Cli/InspectCommands.cs` — inspect group with subcommands

## Crash Detection

- Daemon sets `process.EnableRaisingEvents = true` and handles `process.Exited`
- When netcoredbg dies: daemon sets `_sessionTerminated = true` with exit code
- Next CLI command gets: `"error": "debuggee exited unexpectedly with code <N>"`
- `ndb status` shows: `{"active": true, "status": "terminated", "exitCode": N}`
- RequestDispatcher checks `_sessionTerminated` before dispatching any command

## Testing

### Unit Tests

- **DapClient tests** — mock streams with pre-written responses for new methods (setBreakpoints, stackTrace, continue, etc.)
- **BreakpointManager tests** — add/remove/enable/disable logic, DAP resend behavior, ID assignment

### Integration Tests

- `launch --stop-on-entry` -> `breakpoint set` -> `exec continue --wait` -> verify stopped at breakpoint
- `launch --stop-on-entry` -> `inspect stacktrace` -> verify frames
- `launch --stop-on-entry` -> `inspect variables` -> verify variables visible
- `launch --stop-on-entry` -> `inspect evaluate "1+1"` -> verify result
- `launch --stop-on-entry` -> `exec step-over --wait` -> verify moved to next line
- `attach --pid <PID>` -> `exec pause` -> `inspect threads` -> `stop`
