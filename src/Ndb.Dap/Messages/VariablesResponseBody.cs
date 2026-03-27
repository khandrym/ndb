using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class VariablesResponseBody
{
    [JsonPropertyName("variables")]
    public VariableInfo[] Variables { get; set; } = [];
}

public class VariableInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("variablesReference")]
    public int VariablesReference { get; set; }
}
