using System.Text.Json.Serialization;
// No additional usings needed - SessionInfo is a simple POCO

namespace Ndb.Daemon;

public class SessionInfo
{
    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("pipe")]
    public string Pipe { get; set; } = "";

    [JsonPropertyName("log")]
    public string Log { get; set; } = "";
}
