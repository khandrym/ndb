using System.Text.Json;
using System.Text.Json.Serialization;
using Ndb.Dap.Messages;

namespace Ndb.Dap;

[JsonSerializable(typeof(DapRequest))]
[JsonSerializable(typeof(DapResponse))]
[JsonSerializable(typeof(DapEvent))]
[JsonSerializable(typeof(ProtocolMessage))]
[JsonSerializable(typeof(InitializeArguments))]
[JsonSerializable(typeof(LaunchArguments))]
[JsonSerializable(typeof(DisconnectArguments))]
[JsonSerializable(typeof(Capabilities))]
[JsonSerializable(typeof(JsonElement))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class DapJsonContext : JsonSerializerContext;
