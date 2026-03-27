using System.CommandLine;
using Ndb.Cli;

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

return await rootCommand.Parse(args).InvokeAsync();
