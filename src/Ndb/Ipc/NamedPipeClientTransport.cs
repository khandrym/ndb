using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Ndb.Ipc;

public class NamedPipeClientTransport : ITransport
{
    private readonly NamedPipeClientStream _client;
    public Stream Stream => _client;

    public NamedPipeClientTransport(string pipeName)
    {
        _client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _client.ConnectAsync(500, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}
