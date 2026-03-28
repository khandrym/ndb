using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class CheckUpdateData
{
    [JsonPropertyName("current")]
    public string Current { get; init; } = "";

    [JsonPropertyName("latest")]
    public string? Latest { get; init; }

    [JsonPropertyName("updateAvailable")]
    public bool UpdateAvailable { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}
