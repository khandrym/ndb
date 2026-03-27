using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class EvaluateArguments
{
    [JsonPropertyName("expression")]
    public string Expression { get; set; } = "";

    [JsonPropertyName("frameId")]
    public int FrameId { get; set; }

    [JsonPropertyName("context")]
    public string Context { get; set; } = "repl";
}
