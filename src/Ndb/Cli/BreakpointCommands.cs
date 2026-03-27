using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Json;
using Ndb.Models;

namespace Ndb.Cli;

public static class BreakpointCommands
{
    public static Command Create()
    {
        var group = new Command("breakpoint") { Description = "Manage breakpoints" };

        group.Add(CreateSet());
        group.Add(CreateRemove());
        group.Add(CreateList());
        group.Add(CreateEnable());
        group.Add(CreateDisable());
        group.Add(CreateException());

        return group;
    }

    private static Command CreateSet()
    {
        var fileArg = new Argument<string>("file") { Description = "Source file path" };
        var lineArg = new Argument<int>("line") { Description = "Line number" };
        var conditionOption = new Option<string?>("--condition") { Description = "Conditional expression" };

        var cmd = new Command("set") { Description = "Set a breakpoint" };
        cmd.Add(fileArg);
        cmd.Add(lineArg);
        cmd.Add(conditionOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var file = System.IO.Path.GetFullPath(pr.GetValue(fileArg)!);
            var line = pr.GetValue(lineArg);
            var condition = pr.GetValue(conditionOption);

            var p = new BreakpointSetParams { File = file, Line = line, Condition = condition };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.BreakpointSetParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("breakpoint.set", pJson);
        });
        return cmd;
    }

    private static Command CreateRemove()
    {
        var fileArg = new Argument<string>("file") { Description = "Source file path" };
        var lineArg = new Argument<int>("line") { Description = "Line number" };

        var cmd = new Command("remove") { Description = "Remove a breakpoint" };
        cmd.Add(fileArg);
        cmd.Add(lineArg);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var file = System.IO.Path.GetFullPath(pr.GetValue(fileArg)!);
            var line = pr.GetValue(lineArg);

            var p = new BreakpointRemoveParams { File = file, Line = line };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.BreakpointRemoveParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("breakpoint.remove", pJson);
        });
        return cmd;
    }

    private static Command CreateList()
    {
        var cmd = new Command("list") { Description = "List all breakpoints" };
        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("breakpoint.list");
        });
        return cmd;
    }

    private static Command CreateEnable()
    {
        var idArg = new Argument<int>("id") { Description = "Breakpoint ID" };
        var cmd = new Command("enable") { Description = "Enable a breakpoint" };
        cmd.Add(idArg);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var id = pr.GetValue(idArg);
            var p = new BreakpointIdParams { Id = id };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.BreakpointIdParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("breakpoint.enable", pJson);
        });
        return cmd;
    }

    private static Command CreateDisable()
    {
        var idArg = new Argument<int>("id") { Description = "Breakpoint ID" };
        var cmd = new Command("disable") { Description = "Disable a breakpoint" };
        cmd.Add(idArg);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var id = pr.GetValue(idArg);
            var p = new BreakpointIdParams { Id = id };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.BreakpointIdParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("breakpoint.disable", pJson);
        });
        return cmd;
    }

    private static Command CreateException()
    {
        var filterOption = new Option<string?>("--filter") { Description = "Exception filter: all, user-unhandled" };
        var clearOption = new Option<bool>("--clear") { Description = "Clear all exception filters" };

        var cmd = new Command("exception") { Description = "Set exception breakpoints" };
        cmd.Add(filterOption);
        cmd.Add(clearOption);

        cmd.SetAction(async (ParseResult pr, CancellationToken ct) =>
        {
            var filter = pr.GetValue(filterOption);
            var clear = pr.GetValue(clearOption);

            var p = new ExceptionFilterParams { Filter = filter, Clear = clear };
            var pJson = JsonSerializer.SerializeToElement(p, NdbJsonContext.Default.ExceptionFilterParams);
            Environment.ExitCode = await DaemonConnector.SendCommandAsync("breakpoint.exception", pJson);
        });
        return cmd;
    }
}
