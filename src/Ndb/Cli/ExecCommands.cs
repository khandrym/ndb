using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Json;
using Ndb.Models;

namespace Ndb.Cli;

public static class ExecCommands
{
    public static Command Create()
    {
        var group = new Command("exec") { Description = "Control execution" };

        group.Add(CreateStepCommand("continue", "Resume execution"));
        group.Add(CreatePause());
        group.Add(CreateStepCommand("step-over", "Step over current line"));
        group.Add(CreateStepCommand("step-into", "Step into function call"));
        group.Add(CreateStepCommand("step-out", "Step out of current function"));
        group.Add(CreateRunToCursor());

        return group;
    }

    private static Command CreateStepCommand(string name, string description)
    {
        var waitOption = new Option<bool>("--wait") { Description = "Wait for next stop event" };
        var timeoutOption = new Option<int?>("--timeout") { Description = "Timeout in seconds for --wait" };
        var threadOption = new Option<int>("--thread") { Description = "Thread ID (0 = first thread)" };

        var cmd = new Command(name) { Description = description };
        cmd.Add(waitOption);
        cmd.Add(timeoutOption);
        cmd.Add(threadOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var wait = pr.GetValue(waitOption);
            var timeout = pr.GetValue(timeoutOption);
            var threadId = pr.GetValue(threadOption);

            var p = new ExecParams { ThreadId = threadId, Wait = wait, Timeout = timeout };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.ExecParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync($"exec.{name}", pJson);
        });
        return cmd;
    }

    private static Command CreateRunToCursor()
    {
        var fileArg = new Argument<string>("file") { Description = "Source file path" };
        var lineArg = new Argument<int>("line") { Description = "Line number" };
        var timeoutOption = new Option<int?>("--timeout") { Description = "Timeout in seconds" };

        var cmd = new Command("run-to-cursor") { Description = "Run to a specific line and stop" };
        cmd.Add(fileArg);
        cmd.Add(lineArg);
        cmd.Add(timeoutOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var file = System.IO.Path.GetFullPath(pr.GetValue(fileArg)!);
            var line = pr.GetValue(lineArg);
            var timeout = pr.GetValue(timeoutOption);

            var p = new RunToCursorParams { File = file, Line = line, Timeout = timeout };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.RunToCursorParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("exec.run-to-cursor", pJson);
        });
        return cmd;
    }

    private static Command CreatePause()
    {
        var threadOption = new Option<int>("--thread") { Description = "Thread ID (0 = all threads)" };

        var cmd = new Command("pause") { Description = "Pause execution" };
        cmd.Add(threadOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var threadId = pr.GetValue(threadOption);
            var p = new ExecParams { ThreadId = threadId };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.ExecParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("exec.pause", pJson);
        });
        return cmd;
    }
}
