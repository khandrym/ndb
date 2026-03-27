# ndb — One-Shot CLI for .NET Debugging (AI-Agent Friendly)

## Goal

Provide AI agents (Claude Code, Codex, Copilot, etc.) and automation scripts with a non-interactive CLI for .NET debugging. One command = one action = structured JSON output.

Open-source project for the .NET community.

## Architecture

```
ndb launch app.dll
    |
    +-- spawns --> ndb __daemon --pipe ndb-<pid>
    |                  |
    |              IPC server (Named Pipe / Unix Socket)
    |                  |
    |              DAP Client --stdin/stdout--> netcoredbg --> .NET App
    |
    +-- connects to daemon via IPC
    +-- sends "launch" command
    +-- prints JSON result
    +-- exits

ndb <command>
    |
    +-- reads ~/.ndb/session.json (pipe name)
    +-- connects to daemon via IPC
    +-- sends command
    +-- prints JSON result
    +-- exits
```

### Single Binary

One executable `ndb` with two modes:

- **CLI mode** (default) — one-shot process. Sends command to daemon, prints JSON, exits.
- **Daemon mode** (`ndb __daemon`) — hidden command, long-running. Holds DAP connection to netcoredbg alive between CLI calls. Buffers async DAP events. Auto-started by `ndb launch`/`ndb attach`, exits on `ndb stop` or crash.

Single binary eliminates version mismatch between CLI and daemon — they are always the same build.

### Why Daemon is Needed

DAP is session-oriented: one connection = one session. CLI is one-shot: it exits after each command. The daemon keeps the DAP pipe open and translates one-shot commands into stateful DAP requests.

### No MCP

CLI is the sole interface. Reasons:

- CLI works with ALL agents (any tool with bash access)
- MCP bloats the agent's context window with tool definitions
- MCP adds fragile dependencies and another protocol layer
- Agents are better at calling CLIs than MCPs
- The daemon already solves the statefulness problem that MCP would provide

If the community wants MCP, it can be a thin wrapper — but it is out of scope for this project.

## Tech Stack

- **C# / .NET 10** (LTS) — developer familiarity, Native AOT for fast CLI startup, same ecosystem as debuggee
- **CLI framework**: `System.CommandLine`
- **IPC**: Named Pipes (Windows), Unix Domain Sockets (Linux/macOS) — abstracted via `ITransport` interface
- **DAP client**: custom lightweight implementation (~12 commands, Content-Length + JSON framing)
- **JSON**: `System.Text.Json`
- **netcoredbg**: Samsung's open-source .NET debugger (MIT license)

## Solution Structure

Two projects, one executable:

```
src/
+-- Ndb/           # Exe (Console app, Native AOT) — CLI, daemon, IPC, session management
+-- Ndb.Dap/       # Class library — DAP protocol: framing, messages, client
```

Ndb.Dap is the only truly isolated component (separate protocol, its own framing). Everything else (CLI parsing, daemon loop, IPC) lives in Ndb.

## CLI Commands

### Session (top-level)

| Command | Description | Key flags |
|---|---|---|
| `ndb launch <dll>` | Spawn daemon, start netcoredbg, launch app | `--args`, `--cwd`, `--env KEY=VAL`, `--verbose`, `--stop-on-entry` |
| `ndb attach --pid <PID>` | Spawn daemon, attach to running process | `--verbose` |
| `ndb stop` | Disconnect debugger, stop daemon | — |
| `ndb status` | Daemon alive? Session state? Log path? | — |
| `ndb setup` | Download and install netcoredbg if not found | — |

### Breakpoints (`ndb breakpoint`)

| Command | Description | Key flags |
|---|---|---|
| `ndb breakpoint set <file> <line>` | Set breakpoint | `--condition <expr>` |
| `ndb breakpoint remove <file> <line>` | Remove breakpoint | — |
| `ndb breakpoint list` | List all breakpoints | — |
| `ndb breakpoint enable <id>` | Enable breakpoint | — |
| `ndb breakpoint disable <id>` | Disable breakpoint | — |

### Execution (`ndb exec`)

| Command | Description | Key flags |
|---|---|---|
| `ndb exec continue` | Resume execution | `--wait`, `--timeout <sec>` |
| `ndb exec pause` | Pause execution | — |
| `ndb exec step-over` | Step over line | `--wait`, `--timeout <sec>` |
| `ndb exec step-into` | Step into | `--wait`, `--timeout <sec>` |
| `ndb exec step-out` | Step out | `--wait`, `--timeout <sec>` |

### Inspection (`ndb inspect`)

| Command | Description | Key flags |
|---|---|---|
| `ndb inspect stacktrace` | Call stack | `--thread <id>` |
| `ndb inspect threads` | List threads | — |
| `ndb inspect variables` | Variables in current scope | `--frame <id>`, `--scope <id>` |
| `ndb inspect evaluate <expr>` | Evaluate expression | `--frame <id>` |
| `ndb inspect source <file>` | Show source code | `--line <n>`, `--count <n>` |

### The `--wait` Flag

Key feature for AI agents. Without it, `ndb exec continue` returns immediately with `{"success": true, "data": {"status": "running"}}`. With `--wait`, it blocks until the next `stopped` event and returns the reason, stacktrace, and current line — everything in one call.

`--timeout <sec>` limits wait time. On timeout: `{"success": false, "error": "timeout after 30s, debuggee still running"}`.

## Output Format

All commands output JSON to stdout:

Success:
```json
{
  "success": true,
  "command": "inspect.stacktrace",
  "data": {
    "frames": [
      {"id": 0, "name": "Main", "file": "Program.cs", "line": 42},
      {"id": 1, "name": "Run", "file": "App.cs", "line": 15}
    ]
  }
}
```

Error:
```json
{
  "success": false,
  "command": "exec.continue",
  "error": "debuggee not running"
}
```

### Error Messages

| Situation | Error |
|---|---|
| No active session | `"no active session, use 'ndb launch' to start"` |
| Daemon not responding | `"daemon not responding"` |
| Debuggee exited | `"debuggee exited with code 1"` |
| netcoredbg not found | `"netcoredbg not found, run 'ndb setup' to install"` |
| Breakpoint not verified | `"breakpoint at Program.cs:99 not verified (no executable code)"` |
| Wait timeout | `"timeout after 30s, debuggee still running"` |
| Session already active | `"session already active, use 'ndb stop' first"` |

### Principles

- Always JSON on stdout, even for errors — agents parse predictably
- Error messages include actionable hints
- `--verbose` does NOT affect stdout — verbose output goes only to daemon log file
- CLI stderr — only for fatal errors (can't connect to pipe, invalid arguments) and `ndb setup` progress

## IPC Protocol (CLI <-> Daemon)

### Transport

Named Pipes (Windows) / Unix Domain Sockets (Linux/macOS), abstracted via `ITransport` interface.

### Framing

Content-Length, same as DAP:
```
Content-Length: 72\r\n
\r\n
{"id": 1, "method": "exec.continue", "params": {"wait": true}}
```

### Messages (JSON-RPC-like)

Request (CLI -> Daemon):
```json
{"id": 1, "method": "breakpoint.set", "params": {"file": "Program.cs", "line": 42}}
```

Response (Daemon -> CLI):
```json
{"id": 1, "result": {"id": 1, "verified": true}}
```

Error:
```json
{"id": 1, "error": {"code": -1, "message": "debuggee not running"}}
```

### Method Mapping

CLI command maps directly to method name:
- `ndb breakpoint set` -> `"method": "breakpoint.set"`
- `ndb exec continue --wait` -> `"method": "exec.continue", "params": {"wait": true}`
- `ndb inspect stacktrace --thread 2` -> `"method": "inspect.stacktrace", "params": {"threadId": 2}`
- `ndb stop` -> `"method": "stop"`

### Timeouts

CLI waits for response max 30 seconds (default). `--wait` commands wait until event or `--timeout` value. Ctrl+C cancels the wait.

## DAP Interaction (Daemon <-> netcoredbg)

### Launch

Daemon starts `netcoredbg --interpreter=vscode` as child process. Communication via stdin/stdout with Content-Length + JSON framing (DAP standard).

### DAP Commands in MVP

| CLI Command | DAP Request |
|---|---|
| `launch` | `initialize` -> `launch` -> `configurationDone` |
| `attach` | `initialize` -> `attach` -> `configurationDone` |
| `stop` | `disconnect` (+ kill child) |
| `breakpoint set/remove` | `setBreakpoints` |
| `breakpoint enable/disable` | `setBreakpoints` (resend full list) |
| `exec continue` | `continue` |
| `exec pause` | `pause` |
| `exec step-over/into/out` | `next` / `stepIn` / `stepOut` |
| `inspect stacktrace` | `stackTrace` |
| `inspect variables` | `scopes` -> `variables` |
| `inspect evaluate` | `evaluate` |
| `inspect threads` | `threads` |
| `inspect source` | Direct file read (not DAP) |

### DAP Events

- `stopped` — breakpoint/step/pause/exception -> update session state
- `exited` / `terminated` — mark session as terminated
- `output` — buffer debuggee stdout/stderr

### Breakpoints Note

DAP has no "add/remove one breakpoint". `setBreakpoints` sends the **full list** of breakpoints for a file. Daemon maintains a map `file -> List<Breakpoint>` and resends the complete list on each breakpoint change.

### Source Command

`ndb inspect source` does not use DAP. It reads the file from disk directly. Daemon knows the path from the stack frame and returns the requested fragment.

## Daemon Lifecycle

### Startup (`ndb launch` / `ndb attach`)

1. CLI checks `~/.ndb/session.json`
2. File exists + process alive -> `"error": "session already active, use 'ndb stop' first"`
3. File exists + process dead -> cleanup, continue
4. Spawns `ndb __daemon --pipe ndb-<pid>` as detached process
5. Waits for daemon to accept IPC connection (timeout ~5 sec)
6. Daemon writes `~/.ndb/session.json`:
   ```json
   {"pid": 1234, "pipe": "ndb-1234", "log": "~/.ndb/logs/2026-03-27T14-30-00.log"}
   ```
7. CLI sends launch/attach command, prints result, exits

### Connection (any command except launch/attach/setup)

1. Reads `~/.ndb/session.json`
2. No file -> `"error": "no active session, use 'ndb launch' to start"`
3. Connects to pipe
4. Connection failed -> cleanup PID file, same error
5. Sends command, receives response, prints, exits

### Shutdown (`ndb stop`)

1. CLI sends stop command to daemon
2. Daemon sends DAP `disconnect` to netcoredbg
3. Daemon kills netcoredbg child process (if still alive)
4. Daemon deletes `session.json`
5. Daemon exits

### Crash Recovery

- **netcoredbg crashes**: daemon catches child process exit, marks session as terminated. Next CLI command gets `"error": "debuggee exited unexpectedly"`
- **Daemon crashes**: CLI cannot connect to pipe -> cleanup PID file, clear error message

### Logging

- Path: `~/.ndb/logs/<timestamp>.log`
- Default: ERROR level
- With `--verbose`: DEBUG level (including raw DAP messages)
- Rotation: on launch, delete logs older than 5 sessions
- `ndb status` shows path to current log

## netcoredbg Discovery

`ndb` looks for netcoredbg in this order:

1. `NETCOREDBG_PATH` environment variable
2. `./netcoredbg` (next to the ndb binary)
3. System PATH

If not found: `"error": "netcoredbg not found, run 'ndb setup' to install"`.

### `ndb setup`

Downloads and installs netcoredbg:

1. Queries GitHub Releases API for latest Samsung/netcoredbg release
2. Downloads the archive for current OS + architecture
3. Extracts next to the `ndb` binary
4. Verifies the binary works

## Prerequisites

- .NET SDK 10.0 (for building)
- netcoredbg (auto-installed via `ndb setup`, or manually)
- Target: Windows, Linux, macOS (x64, arm64)

## Development Phases

### Phase 1 — Foundation

- Solution scaffolding: `Ndb` (exe) + `Ndb.Dap` (library)
- Native AOT setup
- IPC transport: `ITransport`, Named Pipes (Windows)
- IPC protocol: Content-Length framing, JSON-RPC messages
- DAP framing + client: `initialize`, `launch`, `disconnect`
- Daemon mode: `ndb __daemon`, session.json (PID file), logging
- CLI: `ndb launch`, `ndb stop`, `ndb status`
- `ndb setup` — netcoredbg download
- Integration test: launch -> status -> stop

### Phase 2 — Core Debugging

- `ndb attach --pid <PID>`
- Breakpoints: `ndb breakpoint set/remove/list/enable/disable`
- Execution: `ndb exec continue/pause/step-over/step-into/step-out`
- `--wait` and `--timeout` for execution commands
- Inspection: `ndb inspect stacktrace/threads/variables/evaluate/source`
- Crash detection and orphan cleanup

### Phase 3 — Polish

- Unix Domain Sockets (Linux/macOS)
- Exception breakpoints
- Nested/complex variables (expand by variablesReference)
- Conditional breakpoints (`--condition`)
- Improved error messages
- Cross-platform CI/CD, GitHub releases (ndb + ndb-bundled)

### Phase 4 — Post-MVP

- Multi-session (parallel daemons)
- Log points, watchpoints
- `ndb exec run-to-cursor <file> <line>`
- Hot reload integration

## References

- [DAP Specification](https://microsoft.github.io/debug-adapter-protocol/specification)
- [netcoredbg GitHub](https://github.com/Samsung/netcoredbg)
- [System.CommandLine](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
