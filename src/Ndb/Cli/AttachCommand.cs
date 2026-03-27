using System;
using System.CommandLine;
using System.CommandLine.Parsing;
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

public static class AttachCommand
{
    public static Command Create()
    {
        var pidOption = new Option<int>("--pid") { Description = "Process ID to attach to", Required = true };
        var verboseOption = new Option<bool>("--verbose") { Description = "Enable debug logging in daemon" };

        var sessionOption = DaemonConnector.CreateSessionOption();

        var command = new Command("attach") { Description = "Attach to a running .NET process" };
        command.Add(pidOption);
        command.Add(verboseOption);
        command.Add(sessionOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var pid = parseResult.GetValue(pidOption);
            var verbose = parseResult.GetValue(verboseOption);
            var session = parseResult.GetValue(sessionOption);
            if (string.IsNullOrEmpty(session)) session = "default";

            var sessionManager = new SessionManager();
            sessionManager.MigrateIfNeeded();
            var existing = sessionManager.LoadAndVerify(session);
            if (existing is not null)
            {
                PrintResponse(NdbResponse.Fail("attach", "session already active, use 'ndb stop' first"));
                return;
            }

            // Verify target process exists
            try { Process.GetProcessById(pid); }
            catch
            {
                PrintResponse(NdbResponse.Fail("attach", $"process {pid} not found"));
                return;
            }

            var pipeName = $"ndb-{session}-{Environment.ProcessId}";
            var ndbAssemblyPath = typeof(AttachCommand).Assembly.Location;
            string daemonFileName;
            string daemonArguments;

            if (!string.IsNullOrEmpty(ndbAssemblyPath) && File.Exists(ndbAssemblyPath))
            {
                // Running as DLL (e.g., dotnet Ndb.dll ...)
                daemonFileName = Environment.ProcessPath!;
                daemonArguments = $"\"{ndbAssemblyPath}\" __daemon --pipe {pipeName} --session {session}";
            }
            else
            {
                // Running as self-contained executable
                daemonFileName = Environment.ProcessPath!;
                daemonArguments = $"__daemon --pipe {pipeName} --session {session}";
            }
            if (verbose) daemonArguments += " --verbose";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = daemonFileName,
                    Arguments = daemonArguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            process.Start();
            process.StandardInput.Close();
            _ = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
            _ = process.StandardError.BaseStream.CopyToAsync(Stream.Null);

            var ready = false;
            for (int i = 0; i < 50; i++)
            {
                await Task.Delay(100, ct);
                try
                {
                    var transport = TransportFactory.CreateClient(pipeName);
                    await using var _ = transport;
                    await transport.ConnectAsync();

                    var attachParams = new AttachParams { ProcessId = pid };
                    var request = new IpcRequest
                    {
                        Id = 1,
                        Method = "attach",
                        Params = JsonSerializer.SerializeToElement(attachParams, NdbJsonContext.Default.AttachParams)
                    };

                    var json = JsonSerializer.Serialize(request, NdbJsonContext.Default.IpcRequest);
                    await IpcFraming.WriteMessageAsync(transport.Stream, json);

                    var responseJson = await IpcFraming.ReadMessageAsync(transport.Stream);
                    if (responseJson is null)
                    {
                        PrintResponse(NdbResponse.Fail("attach", "daemon not responding"));
                        return;
                    }

                    var response = JsonSerializer.Deserialize(responseJson, NdbJsonContext.Default.IpcResponse)!;
                    if (response.Error is not null)
                    {
                        PrintResponse(NdbResponse.Fail("attach", response.Error.Message));
                        return;
                    }

                    PrintResponse(new NdbResponse { Success = true, Command = "attach", Data = response.Result });
                    ready = true;
                    break;
                }
                catch
                {
                    // Daemon not ready yet, retry
                }
            }

            if (!ready)
                PrintResponse(NdbResponse.Fail("attach", "daemon failed to start within 5 seconds"));
        });

        return command;
    }

    private static void PrintResponse(NdbResponse response)
    {
        Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
    }
}
