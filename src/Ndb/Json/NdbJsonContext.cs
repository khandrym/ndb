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
[JsonSerializable(typeof(AttachParams))]
[JsonSerializable(typeof(BreakpointResult))]
[JsonSerializable(typeof(BreakpointListResult))]
[JsonSerializable(typeof(BreakpointSetParams))]
[JsonSerializable(typeof(BreakpointRemoveParams))]
[JsonSerializable(typeof(BreakpointIdParams))]
[JsonSerializable(typeof(ExecParams))]
[JsonSerializable(typeof(ExecResult))]
[JsonSerializable(typeof(FrameSummary))]
[JsonSerializable(typeof(InspectStacktraceParams))]
[JsonSerializable(typeof(InspectStacktraceResult))]
[JsonSerializable(typeof(FrameResult))]
[JsonSerializable(typeof(InspectThreadsResult))]
[JsonSerializable(typeof(ThreadResult))]
[JsonSerializable(typeof(InspectVariablesParams))]
[JsonSerializable(typeof(InspectVariablesResult))]
[JsonSerializable(typeof(VarResult))]
[JsonSerializable(typeof(InspectExpandParams))]
[JsonSerializable(typeof(InspectEvaluateParams))]
[JsonSerializable(typeof(InspectEvaluateResult))]
[JsonSerializable(typeof(InspectSourceParams))]
[JsonSerializable(typeof(InspectSourceResult))]
[JsonSerializable(typeof(ExceptionFilterParams))]
[JsonSerializable(typeof(ExceptionFilterResult))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class NdbJsonContext : JsonSerializerContext;
