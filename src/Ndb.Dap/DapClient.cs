using System.Text.Json;
using Ndb.Dap.Messages;

namespace Ndb.Dap;

/// <summary>
/// DAP client — sends requests to a debug adapter (netcoredbg) and reads responses/events.
/// Thread-safe: reading runs on a background task, writing is synchronized.
/// </summary>
public class DapClient : IDapClient, IDisposable
{
    private readonly Stream _input;   // read from adapter (stdout)
    private readonly Stream _output;  // write to adapter (stdin)
    private int _seq;
    private readonly Dictionary<int, TaskCompletionSource<DapResponse>> _pending = new();
    private readonly Dictionary<int, DapResponse> _bufferedResponses = new();
    private readonly List<DapEvent> _events = new();
    private readonly Task _readLoop;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public IReadOnlyList<DapEvent> Events => _events;

    public DapClient(Stream input, Stream output)
    {
        _input = input;
        _output = output;
        _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
    }

    public async Task<DapResponse> InitializeAsync(CancellationToken ct = default)
    {
        var args = new InitializeArguments();
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.InitializeArguments);
        return await SendRequestAsync("initialize", argsJson, ct);
    }

    public async Task<DapResponse> LaunchAsync(LaunchArguments args, CancellationToken ct = default)
    {
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.LaunchArguments);
        return await SendRequestAsync("launch", argsJson, ct);
    }

    public async Task<DapResponse> ConfigurationDoneAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync("configurationDone", null, ct);
    }

    public async Task<DapResponse> DisconnectAsync(bool terminateDebuggee = true, CancellationToken ct = default)
    {
        var args = new DisconnectArguments { TerminateDebuggee = terminateDebuggee };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.DisconnectArguments);
        return await SendRequestAsync("disconnect", argsJson, ct);
    }

    public async Task<DapResponse> AttachAsync(int processId, CancellationToken ct = default)
    {
        var args = new AttachArguments { ProcessId = processId };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.AttachArguments);
        return await SendRequestAsync("attach", argsJson, ct);
    }

    public async Task<DapResponse> SetBreakpointsAsync(string filePath, SourceBreakpoint[] breakpoints, CancellationToken ct = default)
    {
        var args = new SetBreakpointsArguments
        {
            Source = new Source { Path = filePath },
            Breakpoints = breakpoints
        };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.SetBreakpointsArguments);
        return await SendRequestAsync("setBreakpoints", argsJson, ct);
    }

    public async Task<DapResponse> SetExceptionBreakpointsAsync(string[] filters, CancellationToken ct = default)
    {
        var args = new SetExceptionBreakpointsArguments { Filters = filters };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.SetExceptionBreakpointsArguments);
        return await SendRequestAsync("setExceptionBreakpoints", argsJson, ct);
    }

    public async Task<DapResponse> ContinueAsync(int threadId, CancellationToken ct = default)
    {
        var args = new ContinueArguments { ThreadId = threadId };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.ContinueArguments);
        return await SendRequestAsync("continue", argsJson, ct);
    }

    public async Task<DapResponse> PauseAsync(int threadId, CancellationToken ct = default)
    {
        var args = new PauseArguments { ThreadId = threadId };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.PauseArguments);
        return await SendRequestAsync("pause", argsJson, ct);
    }

    public async Task<DapResponse> NextAsync(int threadId, CancellationToken ct = default)
    {
        var args = new StepArguments { ThreadId = threadId };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.StepArguments);
        return await SendRequestAsync("next", argsJson, ct);
    }

    public async Task<DapResponse> StepInAsync(int threadId, CancellationToken ct = default)
    {
        var args = new StepArguments { ThreadId = threadId };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.StepArguments);
        return await SendRequestAsync("stepIn", argsJson, ct);
    }

    public async Task<DapResponse> StepOutAsync(int threadId, CancellationToken ct = default)
    {
        var args = new StepArguments { ThreadId = threadId };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.StepArguments);
        return await SendRequestAsync("stepOut", argsJson, ct);
    }

    public async Task<DapResponse> StackTraceAsync(int threadId, CancellationToken ct = default)
    {
        var args = new StackTraceArguments { ThreadId = threadId };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.StackTraceArguments);
        return await SendRequestAsync("stackTrace", argsJson, ct);
    }

    public async Task<DapResponse> ThreadsAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync("threads", null, ct);
    }

    public async Task<DapResponse> ScopesAsync(int frameId, CancellationToken ct = default)
    {
        var args = new ScopesArguments { FrameId = frameId };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.ScopesArguments);
        return await SendRequestAsync("scopes", argsJson, ct);
    }

    public async Task<DapResponse> VariablesAsync(int variablesReference, CancellationToken ct = default)
    {
        var args = new VariablesArguments { VariablesReference = variablesReference };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.VariablesArguments);
        return await SendRequestAsync("variables", argsJson, ct);
    }

    public async Task<DapResponse> EvaluateAsync(string expression, int frameId, CancellationToken ct = default)
    {
        var args = new EvaluateArguments { Expression = expression, FrameId = frameId };
        var argsJson = JsonSerializer.SerializeToElement(args, DapJsonContext.Default.EvaluateArguments);
        return await SendRequestAsync("evaluate", argsJson, ct);
    }

    /// <summary>
    /// Waits for a specific event type that arrives AFTER this method is called.
    /// Returns null on timeout. Does not return stale events from before the call.
    /// </summary>
    public async Task<DapEvent?> WaitForEventAsync(string eventName, TimeSpan timeout, CancellationToken ct = default)
    {
        int startIndex;
        lock (_events) { startIndex = _events.Count; }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                lock (_events)
                {
                    for (int i = startIndex; i < _events.Count; i++)
                    {
                        if (_events[i].Event == eventName)
                            return _events[i];
                    }
                }
                await Task.Delay(50, cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        return null;
    }

    public DapEvent? TryGetEvent(string eventName)
    {
        lock (_events)
        {
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                if (_events[i].Event == eventName)
                    return _events[i];
            }
        }
        return null;
    }

    private async Task<DapResponse> SendRequestAsync(string command, JsonElement? arguments, CancellationToken ct)
    {
        var seq = Interlocked.Increment(ref _seq);
        var request = new DapRequest
        {
            Seq = seq,
            Command = command,
            Arguments = arguments
        };

        var tcs = new TaskCompletionSource<DapResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pending)
        {
            // Check if response arrived before we registered
            if (_bufferedResponses.Remove(seq, out var buffered))
            {
                tcs.SetResult(buffered);
            }
            else
            {
                _pending[seq] = tcs;
            }
        }

        var json = JsonSerializer.Serialize(request, DapJsonContext.Default.DapRequest);
        await _writeLock.WaitAsync(ct);
        try
        {
            await DapFraming.WriteMessageAsync(_output, json, ct);
        }
        finally
        {
            _writeLock.Release();
        }

        using var reg = ct.Register(() => tcs.TrySetCanceled());
        return await tcs.Task;
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var json = await DapFraming.ReadMessageAsync(_input, ct);
                if (json is null) break;

                using var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                if (type == "response")
                {
                    var response = JsonSerializer.Deserialize<DapResponse>(json, DapJsonContext.Default.DapResponse)!;
                    lock (_pending)
                    {
                        if (_pending.Remove(response.RequestSeq, out var tcs))
                            tcs.TrySetResult(response);
                        else
                            _bufferedResponses[response.RequestSeq] = response;
                    }
                }
                else if (type == "event")
                {
                    var evt = JsonSerializer.Deserialize<DapEvent>(json, DapJsonContext.Default.DapEvent)!;
                    lock (_events)
                    {
                        _events.Add(evt);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            // Stream closed or read error — complete all pending
            lock (_pending)
            {
                foreach (var tcs in _pending.Values)
                    tcs.TrySetException(new InvalidOperationException("DAP connection closed"));
                _pending.Clear();
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _readLoop.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
        _writeLock.Dispose();
    }
}
