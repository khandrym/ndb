using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class DisconnectArguments
{
    [JsonPropertyName("terminateDebuggee")]
    public bool TerminateDebuggee { get; set; } = true;
}
