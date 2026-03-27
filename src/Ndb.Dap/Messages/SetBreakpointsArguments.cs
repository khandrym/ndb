using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class SetBreakpointsArguments
{
    [JsonPropertyName("source")]
    public Source Source { get; set; } = new();

    [JsonPropertyName("breakpoints")]
    public SourceBreakpoint[] Breakpoints { get; set; } = [];
}

public class Source
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";
}

public class SourceBreakpoint
{
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("condition")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Condition { get; set; }

    [JsonPropertyName("logMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LogMessage { get; set; }
}
