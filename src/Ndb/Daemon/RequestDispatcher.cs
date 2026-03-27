using System;
using System.Collections.Generic;
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

    public RequestDispatcher(DapClient dap, FileLogger logger)
    {
        _dap = dap;
        _logger = logger;
    }

    public async Task<IpcResponse> DispatchAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.Debug($"Dispatching: {request.Method}");
        try
        {
            return request.Method switch
            {
                "launch" => await HandleLaunchAsync(request, ct),
                "stop" => await HandleStopAsync(request, ct),
                "status" => HandleStatus(request),
                _ => IpcResponse.Err(request.Id, -1, $"Unknown method: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Dispatch error: {ex.Message}");
            return IpcResponse.Err(request.Id, -1, ex.Message);
        }
    }

    private async Task<IpcResponse> HandleLaunchAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.Info("Sending initialize to netcoredbg...");
        var initResponse = await _dap.InitializeAsync(ct);
        _logger.Info($"Initialize response: success={initResponse.Success}");
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

        _logger.Info($"Sending launch to netcoredbg: program={launchArgs.Program}");
        var launchResponse = await _dap.LaunchAsync(launchArgs, ct);
        _logger.Info($"Launch response: success={launchResponse.Success}");
        if (!launchResponse.Success)
            return IpcResponse.Err(request.Id, -1, launchResponse.Message ?? "launch failed");

        _logger.Info("Sending configurationDone...");
        await _dap.ConfigurationDoneAsync(ct);
        _logger.Info("ConfigurationDone complete");

        var resultElement = JsonSerializer.SerializeToElement(new CommandStatusData { Status = "running" }, NdbJsonContext.Default.CommandStatusData);
        return IpcResponse.Ok(request.Id, resultElement);
    }

    private async Task<IpcResponse> HandleStopAsync(IpcRequest request, CancellationToken ct)
    {
        await _dap.DisconnectAsync(terminateDebuggee: true, ct);
        var resultElement = JsonSerializer.SerializeToElement(new CommandStatusData { Status = "stopped" }, NdbJsonContext.Default.CommandStatusData);
        return IpcResponse.Ok(request.Id, resultElement);
    }

    private IpcResponse HandleStatus(IpcRequest request)
    {
        var resultElement = JsonSerializer.SerializeToElement(new CommandStatusData { Status = "running" }, NdbJsonContext.Default.CommandStatusData);
        return IpcResponse.Ok(request.Id, resultElement);
    }
}
