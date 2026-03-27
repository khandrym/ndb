using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ndb.Ipc;
using Xunit;

namespace Ndb.Tests;

public class UnixSocketTransportTests
{
    [Fact]
    [Trait("Platform", "Unix")]
    public async Task RoundTrip_ServerAndClient()
    {
        if (OperatingSystem.IsWindows())
            return; // Skip on Windows — run via WSL

        var socketPath = Path.Combine(Path.GetTempPath(), $"ndb-test-{Guid.NewGuid():N}.sock");
        try
        {
            await using var server = new UnixSocketServerTransport(socketPath);

            var acceptTask = server.AcceptAsync();

            await using var client = new UnixSocketClientTransport(socketPath);
            await client.ConnectAsync();

            var serverStream = await acceptTask;

            // Write from client, read from server
            var message = "Hello from client";
            var bytes = Encoding.UTF8.GetBytes(message);
            await client.Stream.WriteAsync(bytes);
            await client.Stream.FlushAsync();

            var buffer = new byte[1024];
            var read = await serverStream.ReadAsync(buffer);
            var received = Encoding.UTF8.GetString(buffer, 0, read);

            Assert.Equal(message, received);
        }
        finally
        {
            if (File.Exists(socketPath))
                File.Delete(socketPath);
        }
    }

    [Fact]
    public void TransportFactory_CreatesCorrectType()
    {
        var server = TransportFactory.CreateServer("test-pipe");
        var client = TransportFactory.CreateClient("test-pipe");

        if (OperatingSystem.IsWindows())
        {
            Assert.IsType<NamedPipeServerTransport>(server);
            Assert.IsType<NamedPipeClientTransport>(client);
        }
        else
        {
            Assert.IsType<UnixSocketServerTransport>(server);
            Assert.IsType<UnixSocketClientTransport>(client);
        }

        (server as IAsyncDisposable)?.DisposeAsync();
        (client as IAsyncDisposable)?.DisposeAsync();
    }
}
