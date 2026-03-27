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
[JsonSerializable(typeof(AttachArguments))]
[JsonSerializable(typeof(SetBreakpointsArguments))]
[JsonSerializable(typeof(SetBreakpointsResponseBody))]
[JsonSerializable(typeof(ContinueArguments))]
[JsonSerializable(typeof(PauseArguments))]
[JsonSerializable(typeof(StepArguments))]
[JsonSerializable(typeof(StackTraceArguments))]
[JsonSerializable(typeof(StackTraceResponseBody))]
[JsonSerializable(typeof(ThreadsResponseBody))]
[JsonSerializable(typeof(ScopesArguments))]
[JsonSerializable(typeof(ScopesResponseBody))]
[JsonSerializable(typeof(VariablesArguments))]
[JsonSerializable(typeof(VariablesResponseBody))]
[JsonSerializable(typeof(EvaluateArguments))]
[JsonSerializable(typeof(EvaluateResponseBody))]
[JsonSerializable(typeof(SetExceptionBreakpointsArguments))]
[JsonSerializable(typeof(JsonElement))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class DapJsonContext : JsonSerializerContext;
