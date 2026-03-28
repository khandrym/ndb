using System;
using System.CommandLine;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Json;

namespace Ndb.Cli;

public static class StopCommand
{
    public static Command Create()
    {
        var sessionOption = DaemonConnector.CreateSessionOption();
        var detachOption = new Option<bool>("--detach") { Description = "Detach without terminating the debuggee" };
        var terminateOption = new Option<bool>("--terminate") { Description = "Terminate the debuggee (default for launch sessions)" };

        var command = new Command("stop", "Stop the debug session and daemon");
        command.Add(sessionOption);
        command.Add(detachOption);
        command.Add(terminateOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            var session = parseResult.GetValue(sessionOption);
            if (string.IsNullOrEmpty(session)) session = "default";

            var detach = parseResult.GetValue(detachOption);
            var terminate = parseResult.GetValue(terminateOption);

            JsonElement? @params = null;
            if (detach || terminate)
            {
                // Explicit override: --detach means terminate=false, --terminate means terminate=true
                @params = JsonSerializer.SerializeToElement(
                    new { terminate = terminate || !detach },
                    NdbJsonContext.Default.JsonElement);
            }

            Environment.ExitCode = await DaemonConnector.SendCommandAsync("stop", @params, session: session);
        });
        return command;
    }
}
