using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text.Json;
using Ndb.Daemon;
using Ndb.Json;
using Ndb.Models;

namespace Ndb.Cli;

public static class StatusCommand
{
    public static Command Create()
    {
        var sessionOption = DaemonConnector.CreateSessionOption();
        var command = new Command("status", "Show current debug session status");
        command.Add(sessionOption);

        command.SetAction((ParseResult parseResult) =>
        {
            var session = parseResult.GetValue(sessionOption);
            var sessionManager = new SessionManager();
            sessionManager.MigrateIfNeeded();

            if (!string.IsNullOrEmpty(session))
            {
                // Show specific session
                var info = sessionManager.LoadAndVerify(session);
                NdbResponse response;
                if (info is null)
                    response = NdbResponse.Ok("status", JsonSerializer.SerializeToElement(
                        new StatusData { Active = false }, NdbJsonContext.Default.StatusData));
                else
                    response = NdbResponse.Ok("status", JsonSerializer.SerializeToElement(
                        new StatusData { Active = true, Pid = info.Pid, Pipe = info.Pipe, Log = info.Log },
                        NdbJsonContext.Default.StatusData));
                Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
            }
            else
            {
                // Show all sessions
                var all = sessionManager.LoadAll();
                var summaries = new List<SessionSummary>();
                foreach (var (name, info) in all)
                {
                    var alive = SessionManager.IsProcessAlive(info.Pid);
                    if (!alive) { sessionManager.Delete(name); continue; }
                    summaries.Add(new SessionSummary
                    {
                        Name = name, Active = true, Pid = info.Pid, Pipe = info.Pipe, Log = info.Log
                    });
                }
                var result = new SessionListResult { Sessions = summaries.ToArray() };
                var response = NdbResponse.Ok("status", JsonSerializer.SerializeToElement(result, NdbJsonContext.Default.SessionListResult));
                Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
            }
        });
        return command;
    }
}
