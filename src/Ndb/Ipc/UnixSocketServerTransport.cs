using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ndb.Ipc;

public class UnixSocketServerTransport : IServerTransport
{
    private readonly string _socketPath;
    private readonly Socket _listener;

    public UnixSocketServerTransport(string socketPath)
    {
        _socketPath = socketPath;

        // Clean up stale socket file
        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(5);
    }

    public async Task<Stream> AcceptAsync(CancellationToken ct = default)
    {
        var client = await _listener.AcceptAsync(ct);
        return new NetworkStream(client, ownsSocket: true);
    }

    public ValueTask DisposeAsync()
    {
        _listener.Dispose();
        try { if (File.Exists(_socketPath)) File.Delete(_socketPath); } catch { }
        return ValueTask.CompletedTask;
    }
}
