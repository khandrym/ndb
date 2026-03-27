using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Dap;
using Ndb.Dap.Messages;
using Ndb.Ipc;
using Ndb.Json;
using Ndb.Models;

namespace Ndb.Daemon;

public class RequestDispatcher
{
    private readonly DapClient _dap;
    private readonly FileLogger _logger;
    private readonly BreakpointManager _breakpoints = new();
    private bool _terminated;
    private int? _exitCode;
    private string[] _exceptionFilters = [];

    public RequestDispatcher(DapClient dap, FileLogger logger)
    {
        _dap = dap;
        _logger = logger;
    }

    public void MarkTerminated(int? exitCode)
    {
        _terminated = true;
        _exitCode = exitCode;
    }

    public bool IsTerminated => _terminated;

    public async Task<IpcResponse> DispatchAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.Debug($"Dispatching: {request.Method}");

        if (_terminated && request.Method != "stop" && request.Method != "status")
        {
            var msg = _exitCode.HasValue
                ? $"debuggee exited with code {_exitCode.Value}"
                : "debuggee exited unexpectedly";
            return IpcResponse.Err(request.Id, -1, msg);
        }

        try
        {
            return request.Method switch
            {
                "launch" => await HandleLaunchAsync(request, ct),
                "attach" => await HandleAttachAsync(request, ct),
                "stop" => await HandleStopAsync(request, ct),
                "status" => HandleStatus(request),

                "breakpoint.set" => await HandleBreakpointSetAsync(request, ct),
                "breakpoint.remove" => await HandleBreakpointRemoveAsync(request, ct),
                "breakpoint.list" => HandleBreakpointList(request),
                "breakpoint.enable" => await HandleBreakpointEnableAsync(request, ct),
                "breakpoint.disable" => await HandleBreakpointDisableAsync(request, ct),
                "breakpoint.exception" => await HandleBreakpointExceptionAsync(request, ct),

                "exec.continue" => await HandleExecAsync(request, "continue", ct),
                "exec.pause" => await HandleExecPauseAsync(request, ct),
                "exec.step-over" => await HandleExecAsync(request, "next", ct),
                "exec.step-into" => await HandleExecAsync(request, "stepIn", ct),
                "exec.step-out" => await HandleExecAsync(request, "stepOut", ct),

                "inspect.stacktrace" => await HandleInspectStacktraceAsync(request, ct),
                "inspect.threads" => await HandleInspectThreadsAsync(request, ct),
                "inspect.variables" => await HandleInspectVariablesAsync(request, ct),
                "inspect.evaluate" => await HandleInspectEvaluateAsync(request, ct),
                "inspect.source" => HandleInspectSource(request),

                _ => IpcResponse.Err(request.Id, -1, $"Unknown method: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Dispatch error: {ex.Message}");
            return IpcResponse.Err(request.Id, -1, ex.Message);
        }
    }

    // --- Session ---

    private async Task<IpcResponse> HandleLaunchAsync(IpcRequest request, CancellationToken ct)
    {
        var initResponse = await _dap.InitializeAsync(ct);
        if (!initResponse.Success)
            return IpcResponse.Err(request.Id, -1, initResponse.Message ?? "initialize failed");

        var launchArgs = new LaunchArguments();
        if (request.Params.HasValue)
        {
            var p = request.Params.Value;
            if (p.TryGetProperty("program", out var prog))
                launchArgs.Program = prog.GetString()!;
            if (p.TryGetProperty("args", out var args))
                launchArgs.Args = args.Deserialize(NdbJsonContext.Default.StringArray);
            if (p.TryGetProperty("cwd", out var cwd))
                launchArgs.Cwd = cwd.GetString();
            if (p.TryGetProperty("stopOnEntry", out var soe))
                launchArgs.StopAtEntry = soe.GetBoolean();
            if (p.TryGetProperty("env", out var env))
                launchArgs.Env = env.Deserialize(NdbJsonContext.Default.DictionaryStringString);
        }

        var launchResponse = await _dap.LaunchAsync(launchArgs, ct);
        if (!launchResponse.Success)
            return IpcResponse.Err(request.Id, -1, launchResponse.Message ?? "launch failed");

        await _dap.ConfigurationDoneAsync(ct);

        var result = new CommandStatusData { Status = "running" };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.CommandStatusData));
    }

    private async Task<IpcResponse> HandleAttachAsync(IpcRequest request, CancellationToken ct)
    {
        var initResponse = await _dap.InitializeAsync(ct);
        if (!initResponse.Success)
            return IpcResponse.Err(request.Id, -1, initResponse.Message ?? "initialize failed");

        var processId = 0;
        if (request.Params.HasValue && request.Params.Value.TryGetProperty("processId", out var pid))
            processId = pid.GetInt32();

        var attachResponse = await _dap.AttachAsync(processId, ct);
        if (!attachResponse.Success)
            return IpcResponse.Err(request.Id, -1, attachResponse.Message ?? "attach failed");

        await _dap.ConfigurationDoneAsync(ct);

        var result = new CommandStatusData { Status = "attached" };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.CommandStatusData));
    }

    private async Task<IpcResponse> HandleStopAsync(IpcRequest request, CancellationToken ct)
    {
        if (!_terminated)
        {
            try { await _dap.DisconnectAsync(terminateDebuggee: true, ct); }
            catch { /* best effort */ }
        }
        var result = new CommandStatusData { Status = "stopped" };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.CommandStatusData));
    }

    private IpcResponse HandleStatus(IpcRequest request)
    {
        var status = _terminated ? "terminated" : "running";
        var result = new CommandStatusData { Status = status };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.CommandStatusData));
    }

    // --- Breakpoints ---

    private async Task<IpcResponse> HandleBreakpointSetAsync(IpcRequest request, CancellationToken ct)
    {
        var p = request.Params!.Value;
        var file = p.GetProperty("file").GetString()!;
        var line = p.GetProperty("line").GetInt32();
        string? condition = null;
        if (p.TryGetProperty("condition", out var cond))
            condition = cond.GetString();

        var bp = _breakpoints.Add(file, line, condition);
        await SyncBreakpointsForFileAsync(file, ct);

        var result = new BreakpointResult
        {
            Id = bp.Id, File = bp.File, Line = bp.Line,
            Verified = bp.Verified, Enabled = bp.Enabled, Condition = bp.Condition
        };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.BreakpointResult));
    }

    private async Task<IpcResponse> HandleBreakpointRemoveAsync(IpcRequest request, CancellationToken ct)
    {
        var p = request.Params!.Value;
        var file = p.GetProperty("file").GetString()!;
        var line = p.GetProperty("line").GetInt32();

        if (!_breakpoints.Remove(file, line))
            return IpcResponse.Err(request.Id, -1, $"no breakpoint at {file}:{line}");

        await SyncBreakpointsForFileAsync(file, ct);

        var result = new CommandStatusData { Status = "removed" };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.CommandStatusData));
    }

    private IpcResponse HandleBreakpointList(IpcRequest request)
    {
        var all = _breakpoints.GetAll();
        var result = new BreakpointListResult
        {
            Breakpoints = all.Select(bp => new BreakpointResult
            {
                Id = bp.Id, File = bp.File, Line = bp.Line,
                Verified = bp.Verified, Enabled = bp.Enabled, Condition = bp.Condition
            }).ToArray()
        };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.BreakpointListResult));
    }

    private async Task<IpcResponse> HandleBreakpointEnableAsync(IpcRequest request, CancellationToken ct)
    {
        var id = request.Params!.Value.GetProperty("id").GetInt32();
        if (!_breakpoints.Enable(id))
            return IpcResponse.Err(request.Id, -1, $"breakpoint {id} not found");

        var file = _breakpoints.GetFileForBreakpoint(id)!;
        await SyncBreakpointsForFileAsync(file, ct);

        var result = new CommandStatusData { Status = "enabled" };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.CommandStatusData));
    }

    private async Task<IpcResponse> HandleBreakpointDisableAsync(IpcRequest request, CancellationToken ct)
    {
        var id = request.Params!.Value.GetProperty("id").GetInt32();
        if (!_breakpoints.Disable(id))
            return IpcResponse.Err(request.Id, -1, $"breakpoint {id} not found");

        var file = _breakpoints.GetFileForBreakpoint(id)!;
        await SyncBreakpointsForFileAsync(file, ct);

        var result = new CommandStatusData { Status = "disabled" };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.CommandStatusData));
    }

    private async Task<IpcResponse> HandleBreakpointExceptionAsync(IpcRequest request, CancellationToken ct)
    {
        var clear = false;
        string? filter = null;

        if (request.Params.HasValue)
        {
            var p = request.Params.Value;
            if (p.TryGetProperty("clear", out var c)) clear = c.GetBoolean();
            if (p.TryGetProperty("filter", out var f)) filter = f.GetString();
        }

        if (clear)
        {
            _exceptionFilters = [];
        }
        else if (filter is not null)
        {
            if (!_exceptionFilters.Contains(filter))
                _exceptionFilters = [.. _exceptionFilters, filter];
        }

        var response = await _dap.SetExceptionBreakpointsAsync(_exceptionFilters, ct);
        if (!response.Success)
            return IpcResponse.Err(request.Id, -1, response.Message ?? "setExceptionBreakpoints failed");

        var result = new ExceptionFilterResult { Filters = _exceptionFilters };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.ExceptionFilterResult));
    }

    private async Task SyncBreakpointsForFileAsync(string file, CancellationToken ct)
    {
        var enabled = _breakpoints.GetEnabledForFile(file);
        var sourceBreakpoints = enabled.Select(bp => new SourceBreakpoint
        {
            Line = bp.Line,
            Condition = bp.Condition
        }).ToArray();

        var response = await _dap.SetBreakpointsAsync(file, sourceBreakpoints, ct);

        if (response.Success && response.Body.HasValue)
        {
            var body = response.Body.Value.Deserialize(DapJsonContext.Default.SetBreakpointsResponseBody);
            if (body?.Breakpoints is not null)
            {
                foreach (var bpInfo in body.Breakpoints)
                {
                    _breakpoints.UpdateVerified(file, bpInfo.Line, bpInfo.Verified);
                }
            }
        }
    }

    // --- Execution ---

    private async Task<IpcResponse> HandleExecAsync(IpcRequest request, string dapCommand, CancellationToken ct)
    {
        var threadId = 0;
        var wait = false;
        int? timeout = null;

        if (request.Params.HasValue)
        {
            var p = request.Params.Value;
            if (p.TryGetProperty("threadId", out var tid)) threadId = tid.GetInt32();
            if (p.TryGetProperty("wait", out var w)) wait = w.GetBoolean();
            if (p.TryGetProperty("timeout", out var t)) timeout = t.GetInt32();
        }

        // Step commands require a valid thread ID — resolve first thread if not specified
        if (threadId == 0 && dapCommand != "continue")
        {
            var threadsResp = await _dap.ThreadsAsync(ct);
            if (threadsResp.Success && threadsResp.Body.HasValue)
            {
                var threads = threadsResp.Body.Value.Deserialize(DapJsonContext.Default.ThreadsResponseBody);
                if (threads?.Threads.Length > 0)
                    threadId = threads.Threads[0].Id;
            }
        }

        DapResponse dapResponse;
        switch (dapCommand)
        {
            case "continue":
                dapResponse = await _dap.ContinueAsync(threadId, ct);
                break;
            case "next":
                dapResponse = await _dap.NextAsync(threadId, ct);
                break;
            case "stepIn":
                dapResponse = await _dap.StepInAsync(threadId, ct);
                break;
            case "stepOut":
                dapResponse = await _dap.StepOutAsync(threadId, ct);
                break;
            default:
                return IpcResponse.Err(request.Id, -1, $"Unknown exec command: {dapCommand}");
        }

        if (!dapResponse.Success)
            return IpcResponse.Err(request.Id, -1, dapResponse.Message ?? $"{dapCommand} failed");

        if (!wait)
        {
            var running = new ExecResult { Status = "running" };
            return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(running, NdbJsonContext.Default.ExecResult));
        }

        // Wait for stopped event
        var timeoutSpan = timeout.HasValue ? TimeSpan.FromSeconds(timeout.Value) : TimeSpan.FromMinutes(10);
        var stoppedEvent = await _dap.WaitForEventAsync("stopped", timeoutSpan, ct);

        if (stoppedEvent is null)
        {
            var timeoutSec = timeout ?? 600;
            return IpcResponse.Err(request.Id, -1, $"timeout after {timeoutSec}s, debuggee still running");
        }

        return BuildStoppedResult(request.Id, stoppedEvent);
    }

    private async Task<IpcResponse> HandleExecPauseAsync(IpcRequest request, CancellationToken ct)
    {
        var threadId = 0;
        if (request.Params.HasValue && request.Params.Value.TryGetProperty("threadId", out var tid))
            threadId = tid.GetInt32();

        var response = await _dap.PauseAsync(threadId, ct);
        if (!response.Success)
            return IpcResponse.Err(request.Id, -1, response.Message ?? "pause failed");

        var result = new ExecResult { Status = "paused" };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.ExecResult));
    }

    private IpcResponse BuildStoppedResult(int requestId, DapEvent stoppedEvent)
    {
        var reason = "unknown";
        int? threadId = null;
        FrameSummary? frame = null;

        if (stoppedEvent.Body.HasValue)
        {
            var body = stoppedEvent.Body.Value;
            if (body.TryGetProperty("reason", out var r)) reason = r.GetString() ?? "unknown";
            if (body.TryGetProperty("threadId", out var t)) threadId = t.GetInt32();
        }

        var result = new ExecResult
        {
            Status = "stopped",
            Reason = reason,
            ThreadId = threadId,
            Frame = frame
        };
        return IpcResponse.Ok(requestId, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.ExecResult));
    }

    // --- Inspection ---

    private async Task<IpcResponse> HandleInspectStacktraceAsync(IpcRequest request, CancellationToken ct)
    {
        var threadId = 0;
        if (request.Params.HasValue && request.Params.Value.TryGetProperty("threadId", out var tid))
            threadId = tid.GetInt32();

        // If threadId is 0, get threads and use the first one
        if (threadId == 0)
        {
            var threadsResp = await _dap.ThreadsAsync(ct);
            if (threadsResp.Success && threadsResp.Body.HasValue)
            {
                var threads = threadsResp.Body.Value.Deserialize(DapJsonContext.Default.ThreadsResponseBody);
                if (threads?.Threads.Length > 0)
                    threadId = threads.Threads[0].Id;
            }
        }

        var response = await _dap.StackTraceAsync(threadId, ct);
        if (!response.Success)
            return IpcResponse.Err(request.Id, -1, response.Message ?? "stackTrace failed");

        var body = response.Body!.Value.Deserialize(DapJsonContext.Default.StackTraceResponseBody);
        var frames = (body?.StackFrames ?? []).Select(f => new FrameResult
        {
            Id = f.Id,
            Name = f.Name,
            File = f.Source?.Path,
            Line = f.Line
        }).ToArray();

        var result = new InspectStacktraceResult { Frames = frames };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.InspectStacktraceResult));
    }

    private async Task<IpcResponse> HandleInspectThreadsAsync(IpcRequest request, CancellationToken ct)
    {
        var response = await _dap.ThreadsAsync(ct);
        if (!response.Success)
            return IpcResponse.Err(request.Id, -1, response.Message ?? "threads failed");

        var body = response.Body!.Value.Deserialize(DapJsonContext.Default.ThreadsResponseBody);
        var threads = (body?.Threads ?? []).Select(t => new ThreadResult { Id = t.Id, Name = t.Name }).ToArray();

        var result = new InspectThreadsResult { Threads = threads };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.InspectThreadsResult));
    }

    private async Task<IpcResponse> HandleInspectVariablesAsync(IpcRequest request, CancellationToken ct)
    {
        int? frameId = null;
        int? scopeIndex = null;
        int? variablesReference = null;

        if (request.Params.HasValue)
        {
            var p = request.Params.Value;
            if (p.TryGetProperty("frameId", out var fid)) frameId = fid.GetInt32();
            if (p.TryGetProperty("scopeIndex", out var si)) scopeIndex = si.GetInt32();
            if (p.TryGetProperty("variablesReference", out var vr)) variablesReference = vr.GetInt32();
        }

        // Direct expand mode
        if (variablesReference.HasValue)
        {
            var varsResp = await _dap.VariablesAsync(variablesReference.Value, ct);
            if (!varsResp.Success)
                return IpcResponse.Err(request.Id, -1, varsResp.Message ?? "variables failed");

            var vars = varsResp.Body!.Value.Deserialize(DapJsonContext.Default.VariablesResponseBody);
            return BuildVariablesResult(request.Id, vars);
        }

        // Standard mode: resolve frame → scopes → variables
        if (!frameId.HasValue)
        {
            var threadsResp = await _dap.ThreadsAsync(ct);
            var threads = threadsResp.Body?.Deserialize(DapJsonContext.Default.ThreadsResponseBody);
            var firstThread = threads?.Threads.FirstOrDefault();
            if (firstThread is null)
                return IpcResponse.Err(request.Id, -1, "no threads available");

            var stResp = await _dap.StackTraceAsync(firstThread.Id, ct);
            var st = stResp.Body?.Deserialize(DapJsonContext.Default.StackTraceResponseBody);
            var topFrame = st?.StackFrames.FirstOrDefault();
            if (topFrame is null)
                return IpcResponse.Err(request.Id, -1, "no frames available");
            frameId = topFrame.Id;
        }

        var scopesResp = await _dap.ScopesAsync(frameId.Value, ct);
        if (!scopesResp.Success)
            return IpcResponse.Err(request.Id, -1, scopesResp.Message ?? "scopes failed");

        var scopes = scopesResp.Body!.Value.Deserialize(DapJsonContext.Default.ScopesResponseBody);
        var targetScope = (scopeIndex.HasValue && scopeIndex.Value < scopes!.Scopes.Length)
            ? scopes.Scopes[scopeIndex.Value]
            : scopes!.Scopes.FirstOrDefault();

        if (targetScope is null)
            return IpcResponse.Err(request.Id, -1, "no scopes available");

        var finalVarsResp = await _dap.VariablesAsync(targetScope.VariablesReference, ct);
        if (!finalVarsResp.Success)
            return IpcResponse.Err(request.Id, -1, finalVarsResp.Message ?? "variables failed");

        var finalVars = finalVarsResp.Body!.Value.Deserialize(DapJsonContext.Default.VariablesResponseBody);
        return BuildVariablesResult(request.Id, finalVars);
    }

    private IpcResponse BuildVariablesResult(int requestId, VariablesResponseBody? vars)
    {
        var variables = (vars?.Variables ?? []).Select(v => new VarResult
        {
            Name = v.Name,
            Value = v.Value,
            Type = v.Type,
            Expandable = v.VariablesReference > 0,
            Ref = v.VariablesReference
        }).ToArray();

        var result = new InspectVariablesResult { Variables = variables };
        return IpcResponse.Ok(requestId, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.InspectVariablesResult));
    }

    private async Task<IpcResponse> HandleInspectEvaluateAsync(IpcRequest request, CancellationToken ct)
    {
        var p = request.Params!.Value;
        var expression = p.GetProperty("expression").GetString()!;
        var frameId = 0;
        if (p.TryGetProperty("frameId", out var fid)) frameId = fid.GetInt32();

        // If no frameId, get top frame
        if (frameId == 0)
        {
            var threadsResp = await _dap.ThreadsAsync(ct);
            var threads = threadsResp.Body?.Deserialize(DapJsonContext.Default.ThreadsResponseBody);
            var firstThread = threads?.Threads.FirstOrDefault();
            if (firstThread is not null)
            {
                var stResp = await _dap.StackTraceAsync(firstThread.Id, ct);
                var st = stResp.Body?.Deserialize(DapJsonContext.Default.StackTraceResponseBody);
                var topFrame = st?.StackFrames.FirstOrDefault();
                if (topFrame is not null) frameId = topFrame.Id;
            }
        }

        var response = await _dap.EvaluateAsync(expression, frameId, ct);
        if (!response.Success)
            return IpcResponse.Err(request.Id, -1, response.Message ?? "evaluate failed");

        var body = response.Body!.Value.Deserialize(DapJsonContext.Default.EvaluateResponseBody);
        var result = new InspectEvaluateResult
        {
            Result = body?.Result ?? "",
            Type = body?.Type
        };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.InspectEvaluateResult));
    }

    private IpcResponse HandleInspectSource(IpcRequest request)
    {
        var p = request.Params!.Value;
        var file = p.GetProperty("file").GetString()!;
        var line = 1;
        var count = 20;
        if (p.TryGetProperty("line", out var l)) line = l.GetInt32();
        if (p.TryGetProperty("count", out var c)) count = c.GetInt32();

        if (!File.Exists(file))
            return IpcResponse.Err(request.Id, -1, $"file not found: {file}");

        var allLines = File.ReadAllLines(file);
        var startIdx = Math.Max(0, line - 1);
        var endIdx = Math.Min(allLines.Length, startIdx + count);
        var lines = allLines[startIdx..endIdx];

        var result = new InspectSourceResult
        {
            File = file,
            StartLine = startIdx + 1,
            Lines = lines
        };
        return IpcResponse.Ok(request.Id, JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.InspectSourceResult));
    }
}
