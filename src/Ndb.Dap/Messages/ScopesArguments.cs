using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class ScopesArguments
{
    [JsonPropertyName("frameId")]
    public int FrameId { get; set; }
}
