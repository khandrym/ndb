using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class SessionListResult
{
    [JsonPropertyName("sessions")]
    public SessionSummary[] Sessions { get; init; } = [];
}

public sealed class SessionSummary
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("active")]
    public bool Active { get; init; }

    [JsonPropertyName("pid")]
    public int Pid { get; init; }

    [JsonPropertyName("pipe")]
    public string Pipe { get; init; } = "";

    [JsonPropertyName("log")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Log { get; init; }
}

public sealed class StatusData
{
    [JsonPropertyName("active")]
    public bool Active { get; init; }

    [JsonPropertyName("pid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Pid { get; init; }

    [JsonPropertyName("pipe")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Pipe { get; init; }

    [JsonPropertyName("log")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Log { get; init; }
}

public sealed class CommandStatusData
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("exitCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExitCode { get; init; }

    [JsonPropertyName("log")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Log { get; init; }
}
