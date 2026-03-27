using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ndb.Daemon;
using Ndb.Ipc;
using Ndb.Models;

namespace Ndb.Json;

[JsonSerializable(typeof(NdbResponse))]
[JsonSerializable(typeof(IpcRequest))]
[JsonSerializable(typeof(IpcResponse))]
[JsonSerializable(typeof(IpcError))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(SessionInfo))]
[JsonSerializable(typeof(StatusData))]
[JsonSerializable(typeof(CommandStatusData))]
[JsonSerializable(typeof(LaunchParams))]
[JsonSerializable(typeof(SetupData))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class NdbJsonContext : JsonSerializerContext;
