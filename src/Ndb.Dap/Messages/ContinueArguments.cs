using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class ContinueArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }
}
