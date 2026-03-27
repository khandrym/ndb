using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class ExceptionFilterParams
{
    [JsonPropertyName("filter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filter { get; init; }

    [JsonPropertyName("clear")]
    public bool Clear { get; init; }
}

public sealed class ExceptionFilterResult
{
    [JsonPropertyName("filters")]
    public string[] Filters { get; init; } = [];
}
