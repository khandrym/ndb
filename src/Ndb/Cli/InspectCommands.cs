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
        var sessionOption = DaemonConnector.CreateSessionOption();
        var cmd = new Command("stacktrace") { Description = "Show call stack" };
        cmd.Add(threadOption);
        cmd.Add(sessionOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var threadId = pr.GetValue(threadOption);
            var session = pr.GetValue(sessionOption);
            if (string.IsNullOrEmpty(session)) session = "default";
            var p = new InspectStacktraceParams { ThreadId = threadId };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.InspectStacktraceParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.stacktrace", pJson, session);
        });
        return cmd;
    }

    private static Command CreateThreads()
    {
        var sessionOption = DaemonConnector.CreateSessionOption();
        var cmd = new Command("threads") { Description = "List all threads" };
        cmd.Add(sessionOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var session = pr.GetValue(sessionOption);
            if (string.IsNullOrEmpty(session)) session = "default";
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.threads", session: session);
        });
        return cmd;
    }

    private static Command CreateVariables()
    {
        var frameOption = new Option<int?>("--frame") { Description = "Frame ID (default: top frame)" };
        var scopeOption = new Option<int?>("--scope") { Description = "Scope index (default: first scope)" };
        var expandOption = new Option<int?>("--expand") { Description = "Expand a variablesReference" };
        var threadOption = new Option<int>("--thread") { Description = "Thread ID (0 = last stopped thread)" };
        var sessionOption = DaemonConnector.CreateSessionOption();
        var cmd = new Command("variables") { Description = "Show variables in scope" };
        cmd.Add(frameOption);
        cmd.Add(scopeOption);
        cmd.Add(expandOption);
        cmd.Add(threadOption);
        cmd.Add(sessionOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var expandRef = pr.GetValue(expandOption);
            var session = pr.GetValue(sessionOption);
            if (string.IsNullOrEmpty(session)) session = "default";

            if (expandRef.HasValue)
            {
                var ep = new InspectExpandParams { VariablesReference = expandRef.Value };
                var epJson = JsonSerializer.SerializeToElement(ep, NdbJsonContext.Default.InspectExpandParams);
                Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.variables", epJson, session);
            }
            else
            {
                var frameId = pr.GetValue(frameOption);
                var scopeIndex = pr.GetValue(scopeOption);
                var threadId = pr.GetValue(threadOption);
                var p = new InspectVariablesParams { FrameId = frameId, ScopeIndex = scopeIndex, ThreadId = threadId };
                var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.InspectVariablesParams);
                Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.variables", pJson, session);
            }
        });
        return cmd;
    }

    private static Command CreateEvaluate()
    {
        var exprArg = new Argument<string>("expression") { Description = "Expression to evaluate" };
        var frameOption = new Option<int>("--frame") { Description = "Frame ID (default: top frame)" };
        var threadOption = new Option<int>("--thread") { Description = "Thread ID (0 = last stopped thread)" };
        var sessionOption = DaemonConnector.CreateSessionOption();
        var cmd = new Command("evaluate") { Description = "Evaluate an expression" };
        cmd.Add(exprArg);
        cmd.Add(frameOption);
        cmd.Add(threadOption);
        cmd.Add(sessionOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var expression = pr.GetValue(exprArg)!;
            var frameId = pr.GetValue(frameOption);
            var threadId = pr.GetValue(threadOption);
            var session = pr.GetValue(sessionOption);
            if (string.IsNullOrEmpty(session)) session = "default";
            var p = new InspectEvaluateParams { Expression = expression, FrameId = frameId, ThreadId = threadId };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.InspectEvaluateParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.evaluate", pJson, session);
        });
        return cmd;
    }

    private static Command CreateSource()
    {
        var fileArg = new Argument<string>("file") { Description = "Source file path" };
        var lineOption = new Option<int>("--line") { Description = "Start line (default: 1)" };
        var countOption = new Option<int>("--count") { Description = "Number of lines (default: 20)" };
        var sessionOption = DaemonConnector.CreateSessionOption();
        var cmd = new Command("source") { Description = "Show source code" };
        cmd.Add(fileArg);
        cmd.Add(lineOption);
        cmd.Add(countOption);
        cmd.Add(sessionOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var file = System.IO.Path.GetFullPath(pr.GetValue(fileArg)!);
            var line = pr.GetValue(lineOption);
            var count = pr.GetValue(countOption);
            var session = pr.GetValue(sessionOption);
            if (string.IsNullOrEmpty(session)) session = "default";
            if (line == 0) line = 1;
            if (count == 0) count = 20;
            var p = new InspectSourceParams { File = file, Line = line, Count = count };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.InspectSourceParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("inspect.source", pJson, session);
        });
        return cmd;
    }
}
