using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Dap;
using Ndb.Ipc;
using Ndb.Json;

namespace Ndb.Daemon;

public class DaemonHost
{
    private readonly string _pipeName;
    private readonly bool _verbose;
    private readonly string _sessionName;
    private readonly SessionManager _sessionManager;

    public DaemonHost(string pipeName, bool verbose, string sessionName = "default", SessionManager? sessionManager = null)
    {
        _pipeName = pipeName;
        _verbose = verbose;
        _sessionName = sessionName;
        _sessionManager = sessionManager ?? new SessionManager();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var logPath = _sessionManager.CreateLogPath();
        _sessionManager.RotateLogs();

        using var logger = new FileLogger(logPath, _verbose);
        logger.Info($"Daemon starting. Pipe: {_pipeName}, PID: {Environment.ProcessId}");

        _sessionManager.Save(_sessionName, new SessionInfo
        {
            Pid = Environment.ProcessId,
            Pipe = _pipeName,
            Log = logPath
        });

        try
        {
            var server = TransportFactory.CreateServer(_pipeName);
            await using var _ = server;

            logger.Info("Waiting for first connection...");
            var firstClientStream = await server.AcceptAsync(ct);
            var firstRequest = await ReadRequestAsync(firstClientStream, ct);

            if (firstRequest is null || (firstRequest.Method != "launch" && firstRequest.Method != "attach"))
            {
                logger.Error($"Expected launch or attach, got: {firstRequest?.Method}");
                await SendResponseAsync(firstClientStream,
                    IpcResponse.Err(firstRequest?.Id ?? 0, -1, "Expected launch or attach as first command"));
                firstClientStream.Dispose();
                return;
            }

            var netcoredbgPath = FindNetcoredbg();
            if (netcoredbgPath is null)
            {
                logger.Error("netcoredbg not found");
                await SendResponseAsync(firstClientStream,
                    IpcResponse.Err(firstRequest.Id, -1, "netcoredbg not found, run 'ndb setup' to install"));
                firstClientStream.Dispose();
                return;
            }

            logger.Info($"Starting netcoredbg: {netcoredbgPath}");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = netcoredbgPath,
                    Arguments = "--interpreter=vscode",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            logger.Info($"netcoredbg started, PID: {process.Id}");

            using var dap = new DapClient(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            var dispatcher = new RequestDispatcher(dap, logger, logPath);

            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                logger.Error($"netcoredbg exited with code {process.ExitCode}");
                dispatcher.MarkTerminated(process.ExitCode);
            };

            var response = await dispatcher.DispatchAsync(firstRequest, ct);
            await SendResponseAsync(firstClientStream, response);
            firstClientStream.Dispose();

            while (!ct.IsCancellationRequested)
            {
                logger.Debug("Waiting for next connection...");
                Stream clientStream;
                try { clientStream = await server.AcceptAsync(ct); }
                catch (OperationCanceledException) { break; }

                try
                {
                    var request = await ReadRequestAsync(clientStream, ct);
                    if (request is null) { clientStream.Dispose(); continue; }

                    logger.Debug($"Received: {request.Method}");
                    var resp = await dispatcher.DispatchAsync(request, ct);
                    await SendResponseAsync(clientStream, resp);

                    if (request.Method == "stop")
                    {
                        logger.Info("Stop requested, shutting down");
                        clientStream.Dispose();
                        break;
                    }
                }
                finally { clientStream.Dispose(); }
            }

            if (!process.HasExited)
            {
                try { process.Kill(); } catch { }
            }
        }
        finally
        {
            _sessionManager.Delete(_sessionName);
            logger.Info("Daemon exiting");
        }
    }

    private static async Task<IpcRequest?> ReadRequestAsync(Stream stream, CancellationToken ct)
    {
        var json = await IpcFraming.ReadMessageAsync(stream, ct);
        if (json is null) return null;
        return JsonSerializer.Deserialize(json, NdbJsonContext.Default.IpcRequest);
    }

    private static async Task SendResponseAsync(Stream stream, IpcResponse response)
    {
        var json = JsonSerializer.Serialize(response, NdbJsonContext.Default.IpcResponse);
        await IpcFraming.WriteMessageAsync(stream, json);
    }

    private static string? FindNetcoredbg()
    {
        var envPath = Environment.GetEnvironmentVariable("NETCOREDBG_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            return envPath;

        var ndbDir = AppContext.BaseDirectory;
        var exeName = OperatingSystem.IsWindows() ? "netcoredbg.exe" : "netcoredbg";
        var localPath = Path.Combine(ndbDir, exeName);
        if (File.Exists(localPath))
            return localPath;

        var subDirPath = Path.Combine(ndbDir, "netcoredbg", exeName);
        if (File.Exists(subDirPath))
            return subDirPath;

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir, exeName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
