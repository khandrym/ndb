using System.Text.Json;
using Ndb.Ipc;
using Ndb.Json;

namespace Ndb.Tests;

public class IpcProtocolTests
{
    [Fact]
    public void IpcRequest_SerializesCorrectly()
    {
        var request = new IpcRequest
        {
            Id = 1,
            Method = "breakpoint.set",
            Params = JsonDocument.Parse("{\"file\":\"Program.cs\",\"line\":42}").RootElement.Clone()
        };
        var json = JsonSerializer.Serialize(request, NdbJsonContext.Default.IpcRequest);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("id").GetInt32());
        Assert.Equal("breakpoint.set", root.GetProperty("method").GetString());
        Assert.Equal("Program.cs", root.GetProperty("params").GetProperty("file").GetString());
    }

    [Fact]
    public void IpcResponse_Success_SerializesCorrectly()
    {
        var resultElement = JsonDocument.Parse("{\"pid\":1234}").RootElement.Clone();
        var response = IpcResponse.Ok(1, resultElement);
        var json = JsonSerializer.Serialize(response, NdbJsonContext.Default.IpcResponse);
        var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(1234, doc.RootElement.GetProperty("result").GetProperty("pid").GetInt32());
    }

    [Fact]
    public void IpcResponse_Error_SerializesCorrectly()
    {
        var response = IpcResponse.Err(1, -1, "not running");
        var json = JsonSerializer.Serialize(response, NdbJsonContext.Default.IpcResponse);
        var doc = JsonDocument.Parse(json);
        Assert.Equal(-1, doc.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal("not running", doc.RootElement.GetProperty("error").GetProperty("message").GetString());
    }
}
