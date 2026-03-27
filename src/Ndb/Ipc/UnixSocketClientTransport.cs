using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ndb.Ipc;

public class UnixSocketClientTransport : ITransport
{
    private readonly Socket _socket;
    private NetworkStream? _stream;

    public Stream Stream => _stream ?? throw new InvalidOperationException("Not connected");

    public string SocketPath { get; }

    public UnixSocketClientTransport(string socketPath)
    {
        SocketPath = socketPath;
        _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), ct);
        _stream = new NetworkStream(_socket, ownsSocket: false);
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
