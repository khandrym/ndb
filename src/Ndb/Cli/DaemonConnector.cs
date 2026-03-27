using System;
using System.CommandLine;
using System.Text.Json;
using System.Threading.Tasks;
using Ndb.Daemon;
using Ndb.Ipc;
using Ndb.Json;
using Ndb.Models;

namespace Ndb.Cli;

public static class DaemonConnector
{
    public static Option<string> CreateSessionOption()
    {
        return new Option<string>("--session") { Description = "Session name (default: default)" };
    }

    public static async Task<int> SendCommandAsync(string method, JsonElement? @params = null, string session = "default")
    {
        if (string.IsNullOrEmpty(session)) session = "default";
        var sessionManager = new SessionManager();
        sessionManager.MigrateIfNeeded();
        var sessionInfo = sessionManager.LoadAndVerify(session);
        if (sessionInfo is null)
        {
            PrintError(method, $"no active session '{session}', use 'ndb launch' to start");
            return 1;
        }

        try
        {
            var transport = TransportFactory.CreateClient(sessionInfo.Pipe);
            await using var _ = transport;
            await transport.ConnectAsync();

            var request = new IpcRequest
            {
                Id = 1,
                Method = method,
                Params = @params
            };

            var json = JsonSerializer.Serialize(request, NdbJsonContext.Default.IpcRequest);
            await IpcFraming.WriteMessageAsync(transport.Stream, json);

            var responseJson = await IpcFraming.ReadMessageAsync(transport.Stream);
            if (responseJson is null)
            {
                PrintError(method, "daemon not responding");
                return 1;
            }

            var response = JsonSerializer.Deserialize(responseJson, NdbJsonContext.Default.IpcResponse)!;

            if (response.Error is not null)
            {
                PrintError(method, response.Error.Message);
                return 1;
            }

            var ndbResponse = new NdbResponse
            {
                Success = true,
                Command = method,
                Data = response.Result
            };
            Console.WriteLine(JsonSerializer.Serialize(ndbResponse, NdbJsonContext.Default.NdbResponse));
            return 0;
        }
        catch (TimeoutException)
        {
            PrintError(method, "daemon not responding");
            return 1;
        }
        catch (Exception ex)
        {
            PrintError(method, ex.Message);
            return 1;
        }
    }

    private static void PrintError(string command, string error)
    {
        var response = NdbResponse.Fail(command, error);
        Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
    }
}
