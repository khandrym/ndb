# check-update Command Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `ndb check-update` command that checks GitHub for newer releases and returns structured JSON.

**Architecture:** New CLI command in `Program.cs` makes a single GET to GitHub releases API, compares version strings, returns result via existing `NdbResponse` pattern. New `CheckUpdateData` model for the response payload.

**Tech Stack:** System.Net.Http, System.Text.Json, GitHub REST API

---

### Task 1: Add CheckUpdateData model

**Files:**
- Create: `src/Ndb/Models/CheckUpdateData.cs`
- Modify: `src/Ndb/Json/NdbJsonContext.cs`

- [ ] **Step 1: Create the model**

```csharp
// src/Ndb/Models/CheckUpdateData.cs
using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class CheckUpdateData
{
    [JsonPropertyName("current")]
    public string Current { get; init; } = "";

    [JsonPropertyName("latest")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Latest { get; init; }

    [JsonPropertyName("updateAvailable")]
    public bool UpdateAvailable { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}
```

- [ ] **Step 2: Register in JSON context**

In `src/Ndb/Json/NdbJsonContext.cs`, add before the `[JsonSourceGenerationOptions]` line:

```csharp
[JsonSerializable(typeof(CheckUpdateData))]
```

- [ ] **Step 3: Verify build**

Run: `dotnet build ndb.slnx`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/Ndb/Models/CheckUpdateData.cs src/Ndb/Json/NdbJsonContext.cs
git commit -m "feat: add CheckUpdateData model for check-update command"
```

---

### Task 2: Add check-update command

**Files:**
- Modify: `src/Ndb/Program.cs`

- [ ] **Step 1: Add the command**

In `src/Ndb/Program.cs`, after the `versionCommand` block and before `return await rootCommand.Parse(args).InvokeAsync();`, add:

```csharp
var checkUpdateCommand = new Command("check-update", "Check for newer ndb version on GitHub");
checkUpdateCommand.SetAction(async (ParseResult _, CancellationToken ct) =>
{
    var current = typeof(LaunchCommand).Assembly.GetName().Version?.ToString() ?? "dev";

    string? latest = null;
    string? error = null;
    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ndb/1.0");
        var json = await http.GetStringAsync("https://api.github.com/repos/khandrym/ndb/releases/latest", ct);
        using var doc = JsonDocument.Parse(json);
        var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        latest = tagName.TrimStart('v');
    }
    catch (Exception ex)
    {
        error = $"failed to reach GitHub API: {ex.Message}";
    }

    var updateAvailable = latest != null && latest != current && string.Compare(latest, current, StringComparison.Ordinal) > 0;

    var data = new CheckUpdateData
    {
        Current = current,
        Latest = latest,
        UpdateAvailable = updateAvailable,
        Error = error
    };
    var response = NdbResponse.Ok("check-update",
        JsonSerializer.SerializeToElement(data, NdbJsonContext.Default.CheckUpdateData));
    Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
});
rootCommand.Add(checkUpdateCommand);
```

Note: `System.Text.Json.JsonDocument` needs `using System.Text.Json;` — already imported in Program.cs.

- [ ] **Step 2: Verify build**

Run: `dotnet build ndb.slnx`
Expected: 0 errors

- [ ] **Step 3: Run tests**

Run: `dotnet test ndb.slnx --filter "FullyQualifiedName!~IntegrationTests"`
Expected: all pass, 0 failures

- [ ] **Step 4: Smoke test**

Run: `dotnet run --project src/Ndb -- check-update`
Expected: JSON with `current`, `latest`, `updateAvailable` fields

- [ ] **Step 5: Commit**

```bash
git add src/Ndb/Program.cs
git commit -m "feat: add ndb check-update command"
```

---

### Task 3: Update README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add to commands table**

In `README.md`, in the Session commands table, after the `ndb version` row, add:

```markdown
| `ndb check-update` | Check for newer ndb version on GitHub |
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add check-update to README commands table"
```

---

### Task 4: Push and verify

- [ ] **Step 1: Push all commits**

```bash
git push
```

- [ ] **Step 2: Verify `--help` shows the command**

Run: `dotnet run --project src/Ndb -- --help`
Expected: `check-update` listed in commands
