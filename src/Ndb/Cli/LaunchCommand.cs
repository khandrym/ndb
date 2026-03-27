using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Daemon;
using Ndb.Ipc;
using Ndb.Json;
using Ndb.Models;

namespace Ndb.Cli;

public static class LaunchCommand
{
    public static Command Create()
    {
        var dllArg = new Argument<string>("dll") { Description = "Path to the .NET DLL to debug" };
        var argsOption = new Option<string[]>("--args") { Description = "Arguments to pass to the debuggee", AllowMultipleArgumentsPerToken = true };
        var cwdOption = new Option<string?>("--cwd") { Description = "Working directory for the debuggee" };
        var verboseOption = new Option<bool>("--verbose") { Description = "Enable debug logging in daemon" };
        var stopOnEntryOption = new Option<bool>("--stop-on-entry") { Description = "Break at entry point" };

        var command = new Command("launch", "Launch a .NET app under the debugger")
        {
            dllArg, argsOption, cwdOption, verboseOption, stopOnEntryOption
        };

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var dll = parseResult.GetValue(dllArg)!;
            var cmdArgs = parseResult.GetValue(argsOption) ?? Array.Empty<string>();
            var cwd = parseResult.GetValue(cwdOption);
            var verbose = parseResult.GetValue(verboseOption);
            var stopOnEntry = parseResult.GetValue(stopOnEntryOption);

            var sessionManager = new SessionManager();

            var existing = sessionManager.LoadAndVerify();
            if (existing is not null)
            {
                PrintResponse(NdbResponse.Fail("launch", "session already active, use 'ndb stop' first"));
                return;
            }

            var dllPath = Path.GetFullPath(dll);
            if (!File.Exists(dllPath))
            {
                PrintResponse(NdbResponse.Fail("launch", $"file not found: {dllPath}"));
                return;
            }

            // Spawn daemon
            var pipeName = $"ndb-{Environment.ProcessId}";
            var ndbPath = Environment.ProcessPath!;
            var daemonArgs = $"__daemon --pipe {pipeName}";
            if (verbose) daemonArgs += " --verbose";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ndbPath,
                    Arguments = daemonArgs,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                }
            };
            process.Start();

            // Wait for daemon to be ready, then send launch command
            var ready = false;
            for (int i = 0; i < 50; i++) // 5 seconds max
            {
                await Task.Delay(100, ct);
                try
                {
                    var transport = new NamedPipeClientTransport(pipeName);
                    await using var _ = transport;
                    await transport.ConnectAsync();

                    var request = new IpcRequest
                    {
                        Id = 1,
                        Method = "launch",
                        Params = JsonSerializer.SerializeToElement(new
                        {
                            program = dllPath,
                            args = cmdArgs.Length > 0 ? cmdArgs : null,
                            cwd = cwd,
                            stopOnEntry = stopOnEntry
                        })
                    };

                    var json = JsonSerializer.Serialize(request, NdbJsonContext.Default.IpcRequest);
                    await IpcFraming.WriteMessageAsync(transport.Stream, json);

                    var responseJson = await IpcFraming.ReadMessageAsync(transport.Stream);
                    if (responseJson is null)
                    {
                        PrintResponse(NdbResponse.Fail("launch", "daemon not responding"));
                        return;
                    }

                    var response = JsonSerializer.Deserialize(responseJson, NdbJsonContext.Default.IpcResponse)!;
                    if (response.Error is not null)
                    {
                        PrintResponse(NdbResponse.Fail("launch", response.Error.Message));
                        return;
                    }

                    PrintResponse(new NdbResponse
                    {
                        Success = true,
                        Command = "launch",
                        Data = response.Result
                    });
                    ready = true;
                    break;
                }
                catch
                {
                    // Daemon not ready yet, retry
                }
            }

            if (!ready)
            {
                PrintResponse(NdbResponse.Fail("launch", "daemon failed to start within 5 seconds"));
            }
        });

        return command;
    }

    private static void PrintResponse(NdbResponse response)
    {
        Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
    }
}
