using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class DapRequest : ProtocolMessage
{
    public DapRequest() { Type = "request"; }

    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Arguments { get; set; }
}
