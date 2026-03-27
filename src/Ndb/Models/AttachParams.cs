using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class AttachParams
{
    [JsonPropertyName("processId")]
    public int ProcessId { get; init; }
}
