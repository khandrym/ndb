using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class Capabilities
{
    [JsonPropertyName("supportsConfigurationDoneRequest")]
    public bool SupportsConfigurationDoneRequest { get; set; }

    [JsonPropertyName("supportsConditionalBreakpoints")]
    public bool SupportsConditionalBreakpoints { get; set; }

    [JsonPropertyName("supportsFunctionBreakpoints")]
    public bool SupportsFunctionBreakpoints { get; set; }
}
