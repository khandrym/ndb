using System.Text.Json;
using System.Text.Json.Serialization;
using Ndb.Ipc;
using Ndb.Models;

namespace Ndb.Json;

[JsonSerializable(typeof(NdbResponse))]
[JsonSerializable(typeof(IpcRequest))]
[JsonSerializable(typeof(IpcResponse))]
[JsonSerializable(typeof(IpcError))]
[JsonSerializable(typeof(JsonElement))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class NdbJsonContext : JsonSerializerContext;
