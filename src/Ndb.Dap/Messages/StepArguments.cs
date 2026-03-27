using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class StepArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }
}
