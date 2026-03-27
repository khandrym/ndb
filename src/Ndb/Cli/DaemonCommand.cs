using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Daemon;

namespace Ndb.Cli;

public static class DaemonCommand
{
    public static Command Create()
    {
        var pipeOption = new Option<string>("--pipe") { Description = "Named pipe name", Required = true };
        var verboseOption = new Option<bool>("--verbose") { Description = "Enable debug logging" };
        var sessionOption = new Option<string>("--session") { Description = "Session name" };

        var command = new Command("__daemon", "Internal: run as daemon process")
        {
            pipeOption,
            verboseOption,
            sessionOption
        };
        command.Hidden = true;

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var pipe = parseResult.GetValue(pipeOption)!;
            var verbose = parseResult.GetValue(verboseOption);
            var session = parseResult.GetValue(sessionOption);
            if (string.IsNullOrEmpty(session)) session = "default";
            var host = new DaemonHost(pipe, verbose, session);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
            await host.RunAsync(cts.Token);
        });

        return command;
    }
}
