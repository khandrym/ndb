using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class ProtocolMessage
{
    [JsonPropertyName("seq")]
    public int Seq { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}
