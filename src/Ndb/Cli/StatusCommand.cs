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

            StatusData statusData;
            if (session is null)
            {
                statusData = new StatusData { Active = false };
            }
            else
            {
                statusData = new StatusData
                {
                    Active = true,
                    Pid = session.Pid,
                    Pipe = session.Pipe,
                    Log = session.Log
                };
            }

            var dataElement = JsonSerializer.SerializeToElement(statusData, NdbJsonContext.Default.StatusData);
            var response = NdbResponse.Ok("status", dataElement);
            Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
        });
        return command;
    }
}
