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
}
