# ndb check-update — Design Spec

## Summary

Add `ndb check-update` command that checks GitHub for newer releases and returns structured JSON with current version, latest version, and update availability.

## Command

```bash
ndb check-update
```

## Behavior

1. Read current version from assembly metadata
2. GET `https://api.github.com/repos/khandrym/ndb/releases/latest`
3. Parse `tag_name` (strip leading "v" — e.g. "v1.0.1" → "1.0.1")
4. Compare versions
5. Return JSON result

## Response Format

**Update available:**
```json
{"success":true,"command":"check-update","data":{"current":"1.0.0","latest":"1.0.1","updateAvailable":true}}
```

**Already up to date:**
```json
{"success":true,"command":"check-update","data":{"current":"1.0.0","latest":"1.0.0","updateAvailable":false}}
```

**Network error (partial response):**
```json
{"success":true,"command":"check-update","data":{"current":"1.0.0","latest":null,"updateAvailable":false,"error":"failed to reach GitHub API"}}
```

## Files to Change

| File | Change |
|---|---|
| `src/Ndb/Models/CheckUpdateData.cs` | New model: `current`, `latest`, `updateAvailable`, `error` |
| `src/Ndb/Json/NdbJsonContext.cs` | Register `CheckUpdateData` |
| `src/Ndb/Program.cs` | Add `check-update` command with async HTTP handler |
| `README.md` | Add to Session commands table |

## Design Decisions

- **Separate command, not a flag on `version`:** command name is self-descriptive for AI agents discovering via `--help`
- **`ndb version` stays offline:** no network dependency for basic version check
- **Partial response on network error:** always returns current version + error explanation, never fails completely
- **Reuses existing pattern:** same HTTP + JSON approach as `SetupCommand.cs`
