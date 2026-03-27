using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ndb.Ipc;

public static class IpcFraming
{
    private const string ContentLengthHeader = "Content-Length: ";

    public static async Task WriteMessageAsync(Stream stream, string body, CancellationToken ct = default)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = $"Content-Length: {bodyBytes.Length}\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, ct);
        await stream.WriteAsync(bodyBytes, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<string?> ReadMessageAsync(Stream stream, CancellationToken ct = default)
    {
        var headerLine = await ReadLineAsync(stream, ct);
        if (headerLine is null) return null;
        if (!headerLine.StartsWith(ContentLengthHeader, StringComparison.Ordinal))
            throw new InvalidDataException($"Expected Content-Length header, got: {headerLine}");
        if (!int.TryParse(headerLine[ContentLengthHeader.Length..], out var contentLength))
            throw new InvalidDataException($"Invalid Content-Length");
        var blank = await ReadLineAsync(stream, ct);
        if (blank is null) return null;
        var buffer = new byte[contentLength];
        var totalRead = 0;
        while (totalRead < contentLength)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, contentLength - totalRead), ct);
            if (read == 0) return null;
            totalRead += read;
        }
        return Encoding.UTF8.GetString(buffer);
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new byte[1];
        var prevWasCr = false;
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) return sb.Length > 0 ? sb.ToString() : null;
            var ch = (char)buffer[0];
            if (ch == '\n' && prevWasCr)
            {
                if (sb.Length > 0 && sb[^1] == '\r') sb.Length--;
                return sb.ToString();
            }
            prevWasCr = ch == '\r';
            sb.Append(ch);
        }
    }
}
