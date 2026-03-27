using System.Text;
using System.Text.Json;
using Ndb.Dap.Messages;

namespace Ndb.Dap.Tests;

public class DapClientTests
{
    [Fact]
    public async Task Initialize_SendsRequestAndReceivesResponse()
    {
        using var clientInput = new MemoryStream();  // client reads from this (adapter stdout)
        using var clientOutput = new MemoryStream();  // client writes to this (adapter stdin)

        // Pre-write the initialize response + initialized event
        var initResponse = """{"seq":1,"type":"response","request_seq":1,"success":true,"command":"initialize","body":{"supportsConfigurationDoneRequest":true}}""";
        var initializedEvent = """{"seq":2,"type":"event","event":"initialized"}""";
        await DapFraming.WriteMessageAsync(clientInput, initResponse);
        await DapFraming.WriteMessageAsync(clientInput, initializedEvent);
        clientInput.Position = 0;

        var client = new DapClient(clientInput, clientOutput);
        var response = await client.InitializeAsync();

        Assert.True(response.Success);
        Assert.Equal("initialize", response.Command);
    }

    [Fact]
    public async Task SendRequest_IncrementsSeq()
    {
        using var clientInput = new MemoryStream();
        using var clientOutput = new MemoryStream();

        // Pre-write two responses
        var resp1 = """{"seq":1,"type":"response","request_seq":1,"success":true,"command":"initialize","body":{}}""";
        var resp2 = """{"seq":2,"type":"event","event":"initialized"}""";
        var resp3 = """{"seq":3,"type":"response","request_seq":2,"success":true,"command":"configurationDone"}""";
        await DapFraming.WriteMessageAsync(clientInput, resp1);
        await DapFraming.WriteMessageAsync(clientInput, resp2);
        await DapFraming.WriteMessageAsync(clientInput, resp3);
        clientInput.Position = 0;

        var client = new DapClient(clientInput, clientOutput);
        await client.InitializeAsync();
        await client.ConfigurationDoneAsync();

        // Verify sent requests have seq 1 and 2
        clientOutput.Position = 0;
        var msg1 = await DapFraming.ReadMessageAsync(clientOutput);
        var msg2 = await DapFraming.ReadMessageAsync(clientOutput);
        var req1 = JsonSerializer.Deserialize<DapRequest>(msg1!, DapJsonContext.Default.DapRequest)!;
        var req2 = JsonSerializer.Deserialize<DapRequest>(msg2!, DapJsonContext.Default.DapRequest)!;
        Assert.Equal(1, req1.Seq);
        Assert.Equal(2, req2.Seq);
    }
}
