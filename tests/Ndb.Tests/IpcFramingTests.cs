using Ndb.Ipc;

namespace Ndb.Tests;

public class IpcFramingTests
{
    [Fact]
    public async Task RoundTrip_WriteAndRead()
    {
        using var stream = new MemoryStream();
        var body = """{"id":1,"method":"launch","params":{}}""";
        await IpcFraming.WriteMessageAsync(stream, body);
        stream.Position = 0;
        var result = await IpcFraming.ReadMessageAsync(stream);
        Assert.Equal(body, result);
    }

    [Fact]
    public async Task ReadMessage_ReturnsNull_OnEmptyStream()
    {
        using var stream = new MemoryStream();
        var result = await IpcFraming.ReadMessageAsync(stream);
        Assert.Null(result);
    }
}
