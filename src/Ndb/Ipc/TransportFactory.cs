using System;
using System.IO;

namespace Ndb.Ipc;

public static class TransportFactory
{
    public static IServerTransport CreateServer(string name)
    {
        if (OperatingSystem.IsWindows())
            return new NamedPipeServerTransport(name);

        var socketPath = GetSocketPath(name);
        return new UnixSocketServerTransport(socketPath);
    }

    public static ITransport CreateClient(string name)
    {
        if (OperatingSystem.IsWindows())
            return new NamedPipeClientTransport(name);

        var socketPath = GetSocketPath(name);
        return new UnixSocketClientTransport(socketPath);
    }

    private static string GetSocketPath(string name)
    {
        var tmpDir = Path.GetTempPath();
        return Path.Combine(tmpDir, $"{name}.sock");
    }
}
