using System;
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
        var command = new Command("status", "Show current debug session status");
        command.SetAction((ParseResult parseResult) =>
        {
            var sessionManager = new SessionManager();
            var session = sessionManager.LoadAndVerify();

            NdbResponse response;
            if (session is null)
            {
                response = NdbResponse.Ok("status", new { active = false });
            }
            else
            {
                response = NdbResponse.Ok("status", new
                {
                    active = true,
                    pid = session.Pid,
                    pipe = session.Pipe,
                    log = session.Log
                });
            }

            Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
        });
        return command;
    }
}
