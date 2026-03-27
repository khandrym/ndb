using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

namespace Ndb.Cli;

public static class StopCommand
{
    public static Command Create()
    {
        var command = new Command("stop", "Stop the debug session and daemon");
        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("stop");
        });
        return command;
    }
}
