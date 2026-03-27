using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class BreakpointResult
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("verified")]
    public bool Verified { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("condition")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Condition { get; init; }
}

public sealed class BreakpointListResult
{
    [JsonPropertyName("breakpoints")]
    public BreakpointResult[] Breakpoints { get; init; } = [];
}

public sealed class BreakpointSetParams
{
    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("condition")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Condition { get; init; }
}

public sealed class BreakpointRemoveParams
{
    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("line")]
    public int Line { get; init; }
}

public sealed class BreakpointIdParams
{
    [JsonPropertyName("id")]
    public int Id { get; init; }
}
