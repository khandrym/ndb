using System;
using System.CommandLine;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Cli;
using Ndb.Json;
using Ndb.Models;

var rootCommand = new RootCommand("ndb — One-shot CLI for .NET debugging");

rootCommand.Add(LaunchCommand.Create());
rootCommand.Add(AttachCommand.Create());
rootCommand.Add(StopCommand.Create());
rootCommand.Add(StatusCommand.Create());
rootCommand.Add(DaemonCommand.Create());
rootCommand.Add(SetupCommand.Create());
rootCommand.Add(BreakpointCommands.Create());
rootCommand.Add(ExecCommands.Create());
rootCommand.Add(InspectCommands.Create());

var versionCommand = new Command("version", "Show ndb version");
versionCommand.SetAction(async (ParseResult _, CancellationToken _) =>
{
    var version = typeof(LaunchCommand).Assembly.GetName().Version?.ToString() ?? "dev";
    var response = NdbResponse.Ok("version",
        JsonSerializer.SerializeToElement(new VersionData { Version = version }, NdbJsonContext.Default.VersionData));
    Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
    await Task.CompletedTask;
});
rootCommand.Add(versionCommand);

return await rootCommand.Parse(args).InvokeAsync();
