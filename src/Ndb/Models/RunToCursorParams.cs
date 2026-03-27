using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class RunToCursorParams
{
    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("timeout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Timeout { get; init; }
}
