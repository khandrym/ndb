using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class AttachArguments
{
    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }
}
