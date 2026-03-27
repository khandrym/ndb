using System.Text.Json;
using Ndb.Dap.Messages;

namespace Ndb.Dap;

/// <summary>
/// DAP client — sends requests to a debug adapter (netcoredbg) and reads responses/events.
/// Thread-safe: reading runs on a background task, writing is synchronized.
/// </summary>
public class DapClient : IDisposable
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
