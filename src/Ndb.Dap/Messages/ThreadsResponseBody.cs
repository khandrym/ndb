using System.Text.Json.Serialization;

namespace Ndb.Dap.Messages;

public class ThreadsResponseBody
{
    [JsonPropertyName("threads")]
    public ThreadInfo[] Threads { get; set; } = [];
}

public class ThreadInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
