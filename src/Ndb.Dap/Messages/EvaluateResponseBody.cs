using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class EvaluateResponseBody
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("variablesReference")]
    public int VariablesReference { get; set; }
}
