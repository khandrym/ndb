using System;
using System.Text.Json;
using System.Threading.Tasks;
using Ndb.Daemon;
using Ndb.Ipc;
using Ndb.Json;
using Ndb.Models;

namespace Ndb.Cli;

public static class DaemonConnector
{
    public static async Task<int> SendCommandAsync(string method, object? @params = null)
    {
        var sessionManager = new SessionManager();
        var session = sessionManager.LoadAndVerify();
        if (session is null)
        {
            PrintError(method, "no active session, use 'ndb launch' to start");
            return 1;
        }

        try
        {
            var transport = new NamedPipeClientTransport(session.Pipe);
            await using var _ = transport;
            await transport.ConnectAsync();

            var request = new IpcRequest
            {
                Id = 1,
                Method = method,
                Params = @params is not null
                    ? JsonSerializer.SerializeToElement(@params)
                    : null
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
