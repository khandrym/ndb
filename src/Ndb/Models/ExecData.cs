using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class ExecParams
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; init; }

    [JsonPropertyName("wait")]
    public bool Wait { get; init; }

    [JsonPropertyName("timeout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Timeout { get; init; }
}

public sealed class ExecResult
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    [JsonPropertyName("threadId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ThreadId { get; init; }

    [JsonPropertyName("frame")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FrameSummary? Frame { get; init; }
}

public sealed class FrameSummary
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? File { get; init; }

    [JsonPropertyName("line")]
    public int Line { get; init; }
}
