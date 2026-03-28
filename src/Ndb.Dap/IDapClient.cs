using Ndb.Dap.Messages;

namespace Ndb.Dap;

public interface IDapClient
{
    IReadOnlyList<DapEvent> Events { get; }

    Task<DapResponse> InitializeAsync(CancellationToken ct = default);
    Task<DapResponse> LaunchAsync(LaunchArguments args, CancellationToken ct = default);
    Task<DapResponse> ConfigurationDoneAsync(CancellationToken ct = default);
    Task<DapResponse> DisconnectAsync(bool terminateDebuggee = true, CancellationToken ct = default);
    Task<DapResponse> AttachAsync(int processId, CancellationToken ct = default);
    Task<DapResponse> SetBreakpointsAsync(string filePath, SourceBreakpoint[] breakpoints, CancellationToken ct = default);
    Task<DapResponse> SetExceptionBreakpointsAsync(string[] filters, CancellationToken ct = default);
    Task<DapResponse> ContinueAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> PauseAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> NextAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> StepInAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> StepOutAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> StackTraceAsync(int threadId, CancellationToken ct = default);
    Task<DapResponse> ThreadsAsync(CancellationToken ct = default);
    Task<DapResponse> ScopesAsync(int frameId, CancellationToken ct = default);
    Task<DapResponse> VariablesAsync(int variablesReference, CancellationToken ct = default);
    Task<DapResponse> EvaluateAsync(string expression, int frameId, CancellationToken ct = default);
    Task<DapEvent?> WaitForEventAsync(string eventName, TimeSpan timeout, CancellationToken ct = default);
}
