# ndb

One-shot CLI for .NET debugging. Built for AI agents.

## Get Started

Paste this into your AI agent chat (Claude Code, Codex, etc.):

```
Install and configure ndb for .NET debugging: https://github.com/khandrym/ndb
```

The agent will read this README, download the release, install everything, and configure your project automatically.

## Setup Instructions for AI Agents

> **For AI agents reading this README:** follow the steps below exactly. Download the release binary — do **not** clone this repository or build from source.

### Step 1: Install ndb

Download the latest **release binary** from [GitHub Releases](https://github.com/khandrym/ndb/releases/latest):

| Platform | Asset | Install command |
|---|---|---|
| Windows x64 | `ndb-win-x64.zip` | Extract to a folder (e.g. `C:\Tools\ndb\`), add folder to PATH |
| Linux x64 | `ndb-linux-x64.tar.gz` | `curl -L https://github.com/khandrym/ndb/releases/latest/download/ndb-linux-x64.tar.gz \| tar xz -C /usr/local/bin` |
| macOS ARM64 | `ndb-osx-arm64.tar.gz` | `curl -L https://github.com/khandrym/ndb/releases/latest/download/ndb-osx-arm64.tar.gz \| tar xz -C /usr/local/bin` |

### Step 2: Install netcoredbg

```bash
ndb setup
```

This downloads [netcoredbg](https://github.com/Samsung/netcoredbg) — the only runtime dependency.

### Step 3: Verify

```bash
ndb version
```

### Step 4: Configure the project

Add this line to the project's `CLAUDE.md`, `AGENTS.md`, or equivalent agent config file:

```markdown
Use `ndb` for .NET debugging. Non-interactive CLI, JSON output. Run `ndb --help` for commands.
```

This ensures all future sessions use ndb automatically. That's it — `--help` is the best way to discover commands and options.

## Commands

Every command outputs structured JSON. One command = one action = one JSON response. No interactive prompts, no TUI — just clean machine-readable output.

```bash
ndb launch MyApp.dll --stop-on-entry
ndb breakpoint set Program.cs 42
ndb exec continue --wait --timeout 30
ndb inspect variables
ndb stop
```

### Session

| Command | Description |
|---|---|
| `ndb launch <dll> [--stop-on-entry] [--breakpoint file:line] [--env KEY=VALUE] [--args ...] [--cwd] [--verbose] [--session name]` | Launch app under debugger |
| `ndb attach --pid <PID> [--session name]` | Attach to running process |
| `ndb stop [--session name]` | Stop debug session |
| `ndb status [--session name]` | Show session status (or list all sessions) |
| `ndb setup` | Download and install netcoredbg |
| `ndb version` | Show ndb version |

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

## Troubleshooting

### Breakpoints show "No symbols have been loaded"

netcoredbg requires **portable** PDB format. If your project uses Windows `full` PDB
(common in older .NET projects), breakpoints will never verify.

**Symptoms:**
- `breakpoint set` always returns `"verified": false`
- `"message": "The breakpoint will not currently be hit. No symbols have been loaded for this document."`
- Breakpoints may still hit despite showing unverified (netcoredbg loads them lazily)
- `inspect evaluate` fails with "does not exist in the current context"

**Solution — override at build time:**
```
dotnet build -c Debug -p:DebugType=portable
```

**Solution — permanent fix in `.csproj`:**
```xml
<PropertyGroup>
  <DebugType>portable</DebugType>
</PropertyGroup>
```

> .NET 5+ projects use portable PDB by default. This issue only affects projects with
> explicit `<DebugType>full</DebugType>` or `<DebugType>pdbonly</DebugType>`.

## Configuration

| Environment Variable | Description |
|---|---|
| `NETCOREDBG_PATH` | Path to netcoredbg binary (overrides auto-detection) |

netcoredbg is discovered in this order:
1. `NETCOREDBG_PATH` environment variable
2. `./netcoredbg` (next to ndb binary)
3. System PATH

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

## Building from Source

> This section is for contributors. If you just want to use ndb, download a [release binary](https://github.com/khandrym/ndb/releases/latest) instead.

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
dotnet test tests/Ndb.Tests
dotnet test tests/Ndb.Dap.Tests

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

## License

MIT
