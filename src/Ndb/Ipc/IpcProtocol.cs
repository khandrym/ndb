using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Ndb.Ipc;

public class IpcRequest
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Params { get; set; }
}

public class IpcResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcError? Error { get; set; }

    public static IpcResponse Ok(int id, object? result = null)
    {
        JsonElement? element = null;
        if (result is not null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(result);
            element = JsonDocument.Parse(bytes).RootElement.Clone();
        }
        return new IpcResponse { Id = id, Result = element };
    }

    public static IpcResponse Err(int id, int code, string message)
    {
        return new IpcResponse { Id = id, Error = new IpcError { Code = code, Message = message } };
    }
}

public class IpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}
