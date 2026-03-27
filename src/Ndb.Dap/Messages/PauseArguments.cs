using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class PauseArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }
}
