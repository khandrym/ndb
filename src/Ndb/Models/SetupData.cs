using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class SetupData
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }
}
