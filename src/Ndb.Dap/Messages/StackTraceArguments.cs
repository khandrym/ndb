using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class StackTraceArguments
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }
}
