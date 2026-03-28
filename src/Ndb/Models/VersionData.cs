using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class VersionData
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";
}
