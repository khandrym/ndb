using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class DapEvent : ProtocolMessage
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("body")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Body { get; set; }
}
