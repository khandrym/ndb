using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class VariablesArguments
{
    [JsonPropertyName("variablesReference")]
    public int VariablesReference { get; set; }
}
