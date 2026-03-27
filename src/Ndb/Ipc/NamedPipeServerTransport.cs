using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Ndb.Ipc;

public class NamedPipeServerTransport : IServerTransport
{
    private readonly string _pipeName;

    public NamedPipeServerTransport(string pipeName)
    {
        _pipeName = pipeName;
    }

    public async Task<Stream> AcceptAsync(CancellationToken ct = default)
    {
        var server = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        await server.WaitForConnectionAsync(ct);
        return server;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
