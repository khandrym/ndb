using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class LaunchParams
{
    [JsonPropertyName("program")]
    public string Program { get; init; } = "";

    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Args { get; init; }

    [JsonPropertyName("cwd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cwd { get; init; }

    [JsonPropertyName("stopOnEntry")]
    public bool StopOnEntry { get; init; }

    [JsonPropertyName("breakpoints")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BreakpointSpec[]? Breakpoints { get; init; }

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Env { get; init; }
}

public sealed class BreakpointSpec
{
    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("condition")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Condition { get; init; }
}
