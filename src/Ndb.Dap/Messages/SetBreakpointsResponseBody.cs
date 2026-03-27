using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class SetBreakpointsResponseBody
{
    [JsonPropertyName("breakpoints")]
    public BreakpointInfo[] Breakpoints { get; set; } = [];
}

public class BreakpointInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }
}
