using System;
using System.CommandLine;
using System.Net.Http;
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

var checkUpdateCommand = new Command("check-update", "Check for newer ndb version on GitHub");
checkUpdateCommand.SetAction(async (ParseResult _, CancellationToken ct) =>
{
    var current = typeof(LaunchCommand).Assembly.GetName().Version?.ToString() ?? "dev";

    string? latest = null;
    string? error = null;
    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ndb/1.0");
        var json = await http.GetStringAsync("https://api.github.com/repos/khandrym/ndb/releases/latest", ct);
        using var doc = JsonDocument.Parse(json);
        var tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        latest = tagName.TrimStart('v');
    }
    catch (Exception ex)
    {
        error = $"failed to reach GitHub API: {ex.Message}";
    }

    var updateAvailable = latest != null && latest != current && string.Compare(latest, current, StringComparison.Ordinal) > 0;

    var data = new CheckUpdateData
    {
        Current = current,
        Latest = latest,
        UpdateAvailable = updateAvailable,
        Error = error
    };
    var response = NdbResponse.Ok("check-update",
        JsonSerializer.SerializeToElement(data, NdbJsonContext.Default.CheckUpdateData));
    Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
});
rootCommand.Add(checkUpdateCommand);

return await rootCommand.Parse(args).InvokeAsync();
