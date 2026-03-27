using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Json;
using Ndb.Models;

namespace Ndb.Cli;

public static class InspectCommands
{
    public static Command Create()
    {
        var group = new Command("inspect") { Description = "Inspect program state" };

        group.Add(CreateStacktrace());
        group.Add(CreateThreads());
        group.Add(CreateVariables());
        group.Add(CreateEvaluate());
        group.Add(CreateSource());

        return group;
    }

    private static Command CreateStacktrace()
    {
        var threadOption = new Option<int>("--thread") { Description = "Thread ID (0 = first thread)" };
        var cmd = new Command("stacktrace") { Description = "Show call stack" };
        cmd.Add(threadOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var threadId = pr.GetValue(threadOption);
            var p = new InspectStacktraceParams { ThreadId = threadId };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.InspectStacktraceParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.stacktrace", pJson);
        });
        return cmd;
    }

    private static Command CreateThreads()
    {
        var cmd = new Command("threads") { Description = "List all threads" };
        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.threads");
        });
        return cmd;
    }

    private static Command CreateVariables()
    {
        var frameOption = new Option<int?>("--frame") { Description = "Frame ID (default: top frame)" };
        var scopeOption = new Option<int?>("--scope") { Description = "Scope index (default: first scope)" };
        var cmd = new Command("variables") { Description = "Show variables in scope" };
        cmd.Add(frameOption);
        cmd.Add(scopeOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var frameId = pr.GetValue(frameOption);
            var scopeIndex = pr.GetValue(scopeOption);
            var p = new InspectVariablesParams { FrameId = frameId, ScopeIndex = scopeIndex };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.InspectVariablesParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.variables", pJson);
        });
        return cmd;
    }

    private static Command CreateEvaluate()
    {
        var exprArg = new Argument<string>("expression") { Description = "Expression to evaluate" };
        var frameOption = new Option<int>("--frame") { Description = "Frame ID (default: top frame)" };
        var cmd = new Command("evaluate") { Description = "Evaluate an expression" };
        cmd.Add(exprArg);
        cmd.Add(frameOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var expression = pr.GetValue(exprArg)!;
            var frameId = pr.GetValue(frameOption);
            var p = new InspectEvaluateParams { Expression = expression, FrameId = frameId };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.InspectEvaluateParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.evaluate", pJson);
        });
        return cmd;
    }

    private static Command CreateSource()
    {
        var fileArg = new Argument<string>("file") { Description = "Source file path" };
        var lineOption = new Option<int>("--line") { Description = "Start line (default: 1)" };
        var countOption = new Option<int>("--count") { Description = "Number of lines (default: 20)" };
        var cmd = new Command("source") { Description = "Show source code" };
        cmd.Add(fileArg);
        cmd.Add(lineOption);
        cmd.Add(countOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var file = System.IO.Path.GetFullPath(pr.GetValue(fileArg)!);
            var line = pr.GetValue(lineOption);
            var count = pr.GetValue(countOption);
            if (line == 0) line = 1;
            if (count == 0) count = 20;
            var p = new InspectSourceParams { File = file, Line = line, Count = count };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.InspectSourceParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.source", pJson);
        });
        return cmd;
    }
}
