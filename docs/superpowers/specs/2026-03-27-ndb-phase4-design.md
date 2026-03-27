# ndb Phase 4 — Post-MVP Design

## Goal

Add advanced features: run-to-cursor, multi-session support, and log points.

Watchpoints and hot reload are out of scope (no netcoredbg/DAP support).

## 1. Run-to-Cursor

### CLI

```
ndb exec run-to-cursor <file> <line> [--timeout <sec>]
```

Equivalent to: set temporary breakpoint → continue --wait → remove breakpoint. One command instead of three.

### Implementation

- CLI sends `exec.run-to-cursor` with file, line, timeout params
- RequestDispatcher handler:
  1. Adds temporary breakpoint via BreakpointManager (flagged as temporary)
  2. Syncs breakpoints with DAP
  3. Sends DAP `continue`
  4. Waits for `stopped` event (with timeout)
  5. Removes temporary breakpoint
  6. Syncs breakpoints with DAP
  7. Returns stopped result (same format as `exec.continue --wait`)

No new DAP commands — composed from existing primitives.

### ManagedBreakpoint Changes

Add `IsTemporary` flag to `ManagedBreakpoint`. Temporary breakpoints are auto-removed after being hit.

## 2. Multi-Session

### Concept

Each debug session has a name. Default name is `default`.

```
ndb launch app.dll --session myapp
ndb breakpoint set Program.cs 42 --session myapp
ndb status                          # shows all sessions
ndb status --session myapp          # shows specific session
ndb stop --session myapp            # stops specific session
```

### Storage

```
~/.ndb/
├── sessions/
│   ├── default.json    # {"pid": 1234, "pipe": "ndb-default-1234", "log": "..."}
│   ├── myapp.json      # {"pid": 5678, "pipe": "ndb-myapp-5678", "log": "..."}
│   └── ...
└── logs/
    └── ...
```

### SessionManager Changes

- `Load(string name = "default")` — loads specific session
- `Save(string name, SessionInfo info)` — saves to sessions/<name>.json
- `Delete(string name)` — deletes specific session
- `LoadAll()` → `Dictionary<string, SessionInfo>` — for `ndb status` (list all)
- `LoadAndVerify(string name)` — loads and checks process alive
- Pipe name format: `ndb-<session>-<pid>`

### CLI Changes

- All commands get `--session <name>` option (default: "default")
- `DaemonConnector.SendCommandAsync` takes session name
- `StatusCommand` without `--session` shows all active sessions
- `LaunchCommand` / `AttachCommand` pass session name to daemon pipe

### Migration

If old `~/.ndb/session.json` exists, treat as `default` session and migrate.

## 3. Log Points

### CLI

```
ndb breakpoint set Program.cs 42 --log-message "x = {x}, y = {y}"
```

When execution reaches the line, netcoredbg evaluates expressions in `{}`, outputs via DAP `output` event, and continues without stopping.

### Implementation

- Add `logMessage` field to `SourceBreakpoint` (Ndb.Dap)
- Add `LogMessage` to `ManagedBreakpoint`
- Add `--log-message` option to `breakpoint set` CLI
- Propagate through: CLI → IPC params → BreakpointManager → SyncBreakpoints → DAP
- Add `logMessage` to `BreakpointResult` response

Minimal changes — one new field through the existing chain.
