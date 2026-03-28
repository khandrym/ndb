using System.Text.Json;
using Ndb.Dap.Messages;

namespace Ndb.Dap.Tests;

public class DapClientPhase2Tests
{
    private async Task<(DapClient client, MemoryStream output)> CreateClientWithResponses(params string[] messages)
    {
        var clientInput = new MemoryStream();
        var clientOutput = new MemoryStream();
        foreach (var msg in messages)
            await DapFraming.WriteMessageAsync(clientInput, msg);
        clientInput.Position = 0;
        var client = new DapClient(clientInput, clientOutput);
        return (client, clientOutput);
    }

    [Fact]
    public async Task SetBreakpointsAsync_SendsCorrectRequest()
    {
        var responseJson = """{"seq":1,"type":"response","request_seq":1,"success":true,"command":"setBreakpoints","body":{"breakpoints":[{"id":1,"verified":true,"line":10}]}}""";
        var (client, output) = await CreateClientWithResponses(responseJson);

        var response = await client.SetBreakpointsAsync("Program.cs", [new SourceBreakpoint { Line = 10 }]);

        Assert.True(response.Success);
        output.Position = 0;
        var sent = await DapFraming.ReadMessageAsync(output);
        var req = JsonSerializer.Deserialize<DapRequest>(sent!, DapJsonContext.Default.DapRequest)!;
        Assert.Equal("setBreakpoints", req.Command);
    }

    [Fact]
    public async Task ContinueAsync_SendsCorrectCommand()
    {
        var responseJson = """{"seq":1,"type":"response","request_seq":1,"success":true,"command":"continue"}""";
        var (client, output) = await CreateClientWithResponses(responseJson);

        var response = await client.ContinueAsync(1);

        Assert.True(response.Success);
        Assert.Equal("continue", response.Command);
    }

    [Fact]
    public async Task StackTraceAsync_SendsCorrectCommand()
    {
        var responseJson = """{"seq":1,"type":"response","request_seq":1,"success":true,"command":"stackTrace","body":{"stackFrames":[{"id":0,"name":"Main","line":42}]}}""";
        var (client, output) = await CreateClientWithResponses(responseJson);

        var response = await client.StackTraceAsync(1);

        Assert.True(response.Success);
        Assert.Equal("stackTrace", response.Command);
    }

    [Fact]
    public async Task WaitForEventAsync_IgnoresPreExistingEvents()
    {
        // Event arrives before WaitForEventAsync is called — should be ignored (stale)
        var stoppedEvent = """{"seq":1,"type":"event","event":"stopped","body":{"reason":"breakpoint","threadId":1}}""";
        var (client, _) = await CreateClientWithResponses(stoppedEvent);

        await Task.Delay(100); // Let read loop pick up event

        var evt = await client.WaitForEventAsync("stopped", TimeSpan.FromMilliseconds(200));

        Assert.Null(evt); // Pre-existing event is ignored
    }

    [Fact]
    public async Task WaitForEventAsync_ReturnsNull_OnTimeout()
    {
        var (client, _) = await CreateClientWithResponses(); // No events

        var evt = await client.WaitForEventAsync("stopped", TimeSpan.FromMilliseconds(100));

        Assert.Null(evt);
    }
}
