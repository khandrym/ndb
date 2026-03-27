using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class SetExceptionBreakpointsArguments
{
    [JsonPropertyName("filters")]
    public string[] Filters { get; set; } = [];
}
