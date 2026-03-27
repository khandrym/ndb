using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

namespace Ndb.Cli;

public static class StopCommand
{
    public static Command Create()
    {
        var sessionOption = DaemonConnector.CreateSessionOption();
        var command = new Command("stop", "Stop the debug session and daemon");
        command.Add(sessionOption);
        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var session = parseResult.GetValue(sessionOption);
            if (string.IsNullOrEmpty(session)) session = "default";
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("stop", session: session);
        });
        return command;
    }
}
