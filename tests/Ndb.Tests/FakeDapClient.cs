using System.Text.Json;
using Ndb.Dap;
using Ndb.Dap.Messages;

namespace Ndb.Tests;

public class FakeDapClient : IDapClient
{
    private readonly List<DapEvent> _events = new();
    public IReadOnlyList<DapEvent> Events => _events;

    public bool? LastDisconnectTerminate { get; private set; }
    public string[]? LastExceptionFilters { get; private set; }
    public int LastContinueThreadId { get; private set; }
    public int LastPauseThreadId { get; private set; }
    public int LastNextThreadId { get; private set; }
    public int LastStackTraceThreadId { get; private set; }

    public DapResponse DefaultResponse { get; set; } = new() { Success = true };
    public DapResponse? ThreadsResponse { get; set; }
    public DapResponse? StackTraceResponse { get; set; }
    public DapEvent? NextStoppedEvent { get; set; }

    public void AddEvent(DapEvent evt) => _events.Add(evt);

    public Task<DapResponse> InitializeAsync(CancellationToken ct = default) => Task.FromResult(DefaultResponse);
    public Task<DapResponse> LaunchAsync(LaunchArguments args, CancellationToken ct = default) => Task.FromResult(DefaultResponse);
    public Task<DapResponse> ConfigurationDoneAsync(CancellationToken ct = default) => Task.FromResult(DefaultResponse);

    public Task<DapResponse> DisconnectAsync(bool terminateDebuggee = true, CancellationToken ct = default)
    {
        LastDisconnectTerminate = terminateDebuggee;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> AttachAsync(int processId, CancellationToken ct = default) => Task.FromResult(DefaultResponse);
    public Task<DapResponse> SetBreakpointsAsync(string filePath, SourceBreakpoint[] breakpoints, CancellationToken ct = default) => Task.FromResult(DefaultResponse);

    public Task<DapResponse> SetExceptionBreakpointsAsync(string[] filters, CancellationToken ct = default)
    {
        LastExceptionFilters = filters;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> ContinueAsync(int threadId, CancellationToken ct = default)
    {
        LastContinueThreadId = threadId;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> PauseAsync(int threadId, CancellationToken ct = default)
    {
        LastPauseThreadId = threadId;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> NextAsync(int threadId, CancellationToken ct = default)
    {
        LastNextThreadId = threadId;
        return Task.FromResult(DefaultResponse);
    }

    public Task<DapResponse> StepInAsync(int threadId, CancellationToken ct = default) => Task.FromResult(DefaultResponse);
    public Task<DapResponse> StepOutAsync(int threadId, CancellationToken ct = default) => Task.FromResult(DefaultResponse);

    public Task<DapResponse> StackTraceAsync(int threadId, CancellationToken ct = default)
    {
        LastStackTraceThreadId = threadId;
        return Task.FromResult(StackTraceResponse ?? DefaultResponse);
    }

    public Task<DapResponse> ThreadsAsync(CancellationToken ct = default) => Task.FromResult(ThreadsResponse ?? DefaultResponse);
    public Task<DapResponse> ScopesAsync(int frameId, CancellationToken ct = default) => Task.FromResult(DefaultResponse);
    public Task<DapResponse> VariablesAsync(int variablesReference, CancellationToken ct = default) => Task.FromResult(DefaultResponse);
    public Task<DapResponse> EvaluateAsync(string expression, int frameId, CancellationToken ct = default) => Task.FromResult(DefaultResponse);

    public Task<DapEvent?> WaitForEventAsync(string eventName, TimeSpan timeout, CancellationToken ct = default)
    {
        var evt = NextStoppedEvent;
        NextStoppedEvent = null;
        return Task.FromResult(evt);
    }
}
