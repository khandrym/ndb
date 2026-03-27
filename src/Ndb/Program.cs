using System.CommandLine;

var rootCommand = new RootCommand("ndb — One-shot CLI for .NET debugging");
return await rootCommand.Parse(args).InvokeAsync();
