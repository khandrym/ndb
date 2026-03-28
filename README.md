# ndb

One-shot CLI for .NET debugging. Built for AI agents.

```
ndb launch MyApp.dll --stop-on-entry
ndb breakpoint set Program.cs 42
ndb exec continue --wait --timeout 30
ndb inspect variables
ndb stop
```

Every command outputs structured JSON. One command = one action = one JSON response. No interactive prompts, no TUI — just clean machine-readable output.

## Installation

Download the latest release for your platform from [GitHub Releases](https://github.com/khandrym/ndb/releases/latest).

**Windows:**
1. Download `ndb-win-x64.zip`
2. Extract to a folder, e.g. `C:\Tools\ndb\`
3. Add the folder to your PATH
4. Open a new terminal and run `ndb setup` to download netcoredbg

**Linux:**
```bash
curl -L https://github.com/khandrym/ndb/releases/latest/download/ndb-linux-x64.tar.gz | tar xz -C /usr/local/bin
ndb setup
```

**macOS (Apple Silicon):**
```bash
curl -L https://github.com/khandrym/ndb/releases/latest/download/ndb-osx-arm64.tar.gz | tar xz -C /usr/local/bin
ndb setup
```

`ndb setup` automatically downloads [netcoredbg](https://github.com/Samsung/netcoredbg) — the only runtime dependency.

## Why

AI agents (Claude Code, Codex, Copilot, etc.) need to debug .NET applications but can't use interactive debuggers like Visual Studio. ndb gives them a non-interactive CLI that speaks JSON, with a daemon that keeps the debug session alive between commands.

## How It Works

```
ndb launch app.dll
    |
    +-- spawns daemon (background process)
    |       |
    |       +-- IPC server (Named Pipe / Unix Socket)
    |       |
    |       +-- DAP client --> netcoredbg --> .NET App
    |
    +-- sends launch command via IPC
    +-- prints JSON result
    +-- exits

ndb <any command>
    |
    +-- connects to daemon via IPC
    +-- sends command, gets response
    +-- prints JSON, exits
```

Single binary. CLI and daemon are the same executable — no version mismatch possible.

## Quick Start

```bash
# 1. Install (or build from source)
ndb setup    # downloads netcoredbg automatically

# 2. Debug
ndb launch bin/Debug/net10.0/MyApp.dll --stop-on-entry
ndb inspect stacktrace
ndb exec step-over --wait
ndb inspect variables
ndb stop
```

## Output Format

All commands return JSON to stdout:

```json
{"success": true, "command": "inspect.stacktrace", "data": {"frames": [...]}}
{"success": false, "command": "launch", "error": "netcoredbg not found, run 'ndb setup' to install"}
```

## Commands

### Session

| Command | Description |
|---|---|
| `ndb launch <dll> [--stop-on-entry] [--args ...] [--cwd] [--verbose] [--session name]` | Launch app under debugger |
| `ndb attach --pid <PID> [--session name]` | Attach to running process |
| `ndb stop [--session name]` | Stop debug session |
| `ndb status [--session name]` | Show session status (or list all sessions) |
| `ndb setup` | Download and install netcoredbg |

### Breakpoints

| Command | Description |
|---|---|
| `ndb breakpoint set <file> <line> [--condition <expr>] [--log-message <msg>]` | Set breakpoint (with optional condition or log message) |
| `ndb breakpoint remove <file> <line>` | Remove breakpoint |
| `ndb breakpoint list` | List all breakpoints |
| `ndb breakpoint enable <id>` | Enable breakpoint |
| `ndb breakpoint disable <id>` | Disable breakpoint |
| `ndb breakpoint exception --filter <filter>` | Break on exceptions (`all`, `user-unhandled`) |
| `ndb breakpoint exception --clear` | Clear exception filters |

### Execution

| Command | Description |
|---|---|
| `ndb exec continue [--wait] [--timeout <sec>]` | Resume execution |
| `ndb exec pause` | Pause execution |
| `ndb exec step-over [--wait] [--timeout <sec>]` | Step over |
| `ndb exec step-into [--wait] [--timeout <sec>]` | Step into |
| `ndb exec step-out [--wait] [--timeout <sec>]` | Step out |
| `ndb exec run-to-cursor <file> <line> [--timeout <sec>]` | Run to specific line |

The `--wait` flag blocks until the debuggee stops (breakpoint, step complete, exception). Returns the stop reason, thread ID, and current frame — everything in one call.

### Inspection

| Command | Description |
|---|---|
| `ndb inspect stacktrace [--thread <id>]` | Show call stack |
| `ndb inspect threads` | List threads |
| `ndb inspect variables [--frame <id>] [--scope <idx>] [--expand <ref>]` | Show variables (use `--expand` for nested objects) |
| `ndb inspect evaluate <expr> [--frame <id>]` | Evaluate expression |
| `ndb inspect source <file> [--line <n>] [--count <n>]` | Show source code |

### Multi-Session

Debug multiple apps simultaneously:

```bash
ndb launch api.dll --session api
ndb launch worker.dll --session worker
ndb status                              # lists both sessions
ndb breakpoint set Api.cs 42 --session api
ndb inspect variables --session worker
ndb stop --session api
ndb stop --session worker
```

## Usage with AI Agents

ndb is designed for AI agents that have shell/bash access. The agent doesn't need any special SDK or plugin — just the ability to run CLI commands and parse JSON output.

### Claude Code

Add to your project's `CLAUDE.md`:

```markdown
## Debugging .NET

Use `ndb` for debugging. It's a one-shot CLI — each command prints JSON to stdout.

Typical workflow:
1. `ndb launch bin/Debug/net10.0/MyApp.dll --stop-on-entry` — start debugging
2. `ndb breakpoint set Program.cs 42` — set breakpoint
3. `ndb exec continue --wait --timeout 30` — run until breakpoint hit
4. `ndb inspect stacktrace` — see where you are
5. `ndb inspect variables` — see local variables
6. `ndb inspect evaluate "myVar.ToString()"` — evaluate expression
7. `ndb exec step-over --wait` — step to next line
8. `ndb stop` — end session

All output is JSON: `{"success": true, "command": "...", "data": {...}}`
```

### Codex (OpenAI)

Add to your project's `AGENTS.md`:

```markdown
## Debugging .NET

Use `ndb` CLI for debugging .NET applications. Each command is non-interactive and outputs JSON to stdout.

### Available commands

- `ndb launch <dll> [--stop-on-entry]` — start debugging
- `ndb attach --pid <PID>` — attach to running process
- `ndb stop` — stop debug session
- `ndb status` — show session info
- `ndb breakpoint set <file> <line> [--condition <expr>]` — set breakpoint
- `ndb breakpoint remove <file> <line>` — remove breakpoint
- `ndb breakpoint list` — list all breakpoints
- `ndb exec continue [--wait] [--timeout <sec>]` — resume execution
- `ndb exec step-over [--wait]` — step over
- `ndb exec step-into [--wait]` — step into
- `ndb exec step-out [--wait]` — step out
- `ndb exec run-to-cursor <file> <line>` — run to line
- `ndb inspect stacktrace` — call stack
- `ndb inspect variables` — local variables
- `ndb inspect evaluate <expr>` — evaluate expression
- `ndb inspect threads` — list threads
- `ndb inspect source <file> [--line <n>] [--count <n>]` — view source

### Output format

All commands return JSON: `{"success": bool, "command": "...", "data": {...}}` or `{"success": false, "error": "..."}`.

### Typical debugging session

1. Build: `dotnet build`
2. Launch: `ndb launch bin/Debug/net10.0/App.dll --stop-on-entry`
3. Set breakpoint: `ndb breakpoint set Program.cs 42`
4. Continue: `ndb exec continue --wait --timeout 30`
5. Inspect: `ndb inspect variables`
6. Stop: `ndb stop`
```

### Other AI Agents (Copilot, Cursor, etc.)

Any agent that can execute shell commands and read stdout works with ndb. No configuration needed — just ensure `ndb` is in PATH and run commands. The `--help` flag on any command provides usage info:

```bash
ndb --help
ndb breakpoint --help
ndb exec --help
ndb inspect --help
```

### Debugging Workflow for Agents

A typical AI agent debugging session:

```bash
# 1. Build the project
dotnet build

# 2. Start debugging with stop-on-entry
ndb launch bin/Debug/net10.0/MyApp.dll --stop-on-entry

# 3. Set breakpoint where the bug might be
ndb breakpoint set Controllers/UserController.cs 87

# 4. Continue to the breakpoint
ndb exec continue --wait --timeout 60

# 5. Inspect state at breakpoint
ndb inspect stacktrace
ndb inspect variables
ndb inspect evaluate "users.Count"

# 6. Step through code
ndb exec step-over --wait
ndb inspect variables

# 7. Done
ndb stop
```

## Building from Source

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
- [netcoredbg](https://github.com/Samsung/netcoredbg) (auto-installed via `ndb setup`)

### Build

```bash
git clone https://github.com/khandrym/ndb.git
cd ndb
dotnet build ndb.slnx
```

### Run

```bash
dotnet run --project src/Ndb -- launch MyApp.dll --stop-on-entry
```

### Publish (Native AOT)

```bash
dotnet publish src/Ndb/Ndb.csproj -c Release -r win-x64 /p:PublishAot=true -o publish/
```

### Test

```bash
# Unit tests
dotnet test tests/Ndb.Tests tests/Ndb.Dap.Tests

# Integration tests (requires netcoredbg)
NETCOREDBG_PATH=/path/to/netcoredbg dotnet test tests/Ndb.IntegrationTests
```

## Architecture

```
src/
  Ndb/           # CLI + daemon (single binary)
    Cli/         # Command definitions (System.CommandLine)
    Daemon/      # Daemon host, session manager, breakpoint manager
    Ipc/         # Named Pipes / Unix Sockets, Content-Length framing
    Models/      # JSON response types
    Json/        # System.Text.Json source generators
  Ndb.Dap/       # DAP protocol library
    Messages/    # DAP request/response/event types
```

- **IPC Protocol:** JSON-RPC over Named Pipes (Windows) / Unix Domain Sockets (Linux/macOS)
- **DAP Protocol:** Content-Length framing over stdin/stdout to netcoredbg
- **Serialization:** System.Text.Json with source generators (Native AOT compatible)

## Configuration

| Environment Variable | Description |
|---|---|
| `NETCOREDBG_PATH` | Path to netcoredbg binary (overrides auto-detection) |

netcoredbg is discovered in this order:
1. `NETCOREDBG_PATH` environment variable
2. `./netcoredbg` (next to ndb binary)
3. System PATH

## License

MIT
