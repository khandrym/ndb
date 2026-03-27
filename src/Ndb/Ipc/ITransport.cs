using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ndb.Ipc;

public interface ITransport : IAsyncDisposable
{
    Stream Stream { get; }
    Task ConnectAsync(CancellationToken ct = default);
}

public interface IServerTransport : IAsyncDisposable
{
    Task<Stream> AcceptAsync(CancellationToken ct = default);
}
