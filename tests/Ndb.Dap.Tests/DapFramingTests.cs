using System.Text;

namespace Ndb.Dap.Tests;

public class DapFramingTests
{
    [Fact]
    public async Task WriteMessage_WritesContentLengthFramedMessage()
    {
        using var stream = new MemoryStream();
        var body = """{"seq":1,"type":"request","command":"initialize"}""";

        await DapFraming.WriteMessageAsync(stream, body);

        stream.Position = 0;
        var result = Encoding.UTF8.GetString(stream.ToArray());
        var expectedLength = Encoding.UTF8.GetByteCount(body);
        Assert.Equal($"Content-Length: {expectedLength}\r\n\r\n{body}", result);
    }

    [Fact]
    public async Task ReadMessage_ReadsContentLengthFramedMessage()
    {
        var body = """{"seq":1,"type":"request","command":"initialize"}""";
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = $"Content-Length: {bodyBytes.Length}\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);

        using var stream = new MemoryStream([.. headerBytes, .. bodyBytes]);

        var result = await DapFraming.ReadMessageAsync(stream);
        Assert.Equal(body, result);
    }

    [Fact]
    public async Task ReadMessage_ReturnsNull_OnEndOfStream()
    {
        using var stream = new MemoryStream();
        var result = await DapFraming.ReadMessageAsync(stream);
        Assert.Null(result);
    }

    [Fact]
    public async Task RoundTrip_WriteAndRead()
    {
        using var stream = new MemoryStream();
        var body = """{"type":"event","event":"stopped","body":{"reason":"breakpoint"}}""";

        await DapFraming.WriteMessageAsync(stream, body);
        stream.Position = 0;

        var result = await DapFraming.ReadMessageAsync(stream);
        Assert.Equal(body, result);
    }
}
