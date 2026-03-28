using System.Text.Json.Serialization;

namespace Ndb.Models;

public sealed class InspectStacktraceParams
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; init; }
}

public sealed class InspectStacktraceResult
{
    [JsonPropertyName("frames")]
    public FrameResult[] Frames { get; init; } = [];
}

public sealed class FrameResult
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? File { get; init; }

    [JsonPropertyName("line")]
    public int Line { get; init; }
}

public sealed class InspectThreadsResult
{
    [JsonPropertyName("threads")]
    public ThreadResult[] Threads { get; init; } = [];
}

public sealed class ThreadResult
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

public sealed class InspectVariablesParams
{
    [JsonPropertyName("threadId")]
    public int ThreadId { get; init; }

    [JsonPropertyName("frameId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FrameId { get; init; }

    [JsonPropertyName("scopeIndex")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ScopeIndex { get; init; }
}

public sealed class InspectVariablesResult
{
    [JsonPropertyName("variables")]
    public VarResult[] Variables { get; init; } = [];
}

public sealed class VarResult
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("value")]
    public string Value { get; init; } = "";

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }

    [JsonPropertyName("expandable")]
    public bool Expandable { get; init; }

    [JsonPropertyName("ref")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Ref { get; init; }
}

public sealed class InspectExpandParams
{
    [JsonPropertyName("variablesReference")]
    public int VariablesReference { get; init; }
}

public sealed class InspectEvaluateParams
{
    [JsonPropertyName("expression")]
    public string Expression { get; init; } = "";

    [JsonPropertyName("frameId")]
    public int FrameId { get; init; }

    [JsonPropertyName("threadId")]
    public int ThreadId { get; init; }
}

public sealed class InspectEvaluateResult
{
    [JsonPropertyName("result")]
    public string Result { get; init; } = "";

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }
}

public sealed class InspectSourceParams
{
    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; } = 20;
}

public sealed class InspectSourceResult
{
    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("startLine")]
    public int StartLine { get; init; }

    [JsonPropertyName("lines")]
    public string[] Lines { get; init; } = [];
}
