using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class LaunchArguments
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "coreclr";

    [JsonPropertyName("program")]
    public string Program { get; set; } = "";

    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Args { get; set; }

    [JsonPropertyName("cwd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cwd { get; set; }

    [JsonPropertyName("stopAtEntry")]
    public bool StopAtEntry { get; set; }

    [JsonPropertyName("console")]
    public string Console { get; set; } = "internalConsole";

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Env { get; set; }
}
