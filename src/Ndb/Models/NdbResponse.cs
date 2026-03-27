using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class NdbResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("command")]
    public string Command { get; init; } = "";

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Data { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    public static NdbResponse Ok(string command, object? data = null)
    {
        JsonElement? element = null;
        if (data is not null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(data);
            element = JsonDocument.Parse(bytes).RootElement.Clone();
        }
        return new NdbResponse { Success = true, Command = command, Data = element };
    }

    public static NdbResponse Fail(string command, string error)
    {
        return new NdbResponse { Success = false, Command = command, Error = error };
    }
}
