using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ndb.IntegrationTests;

public static class TestHelpers
{
    private static readonly string NetcoredbgPath = @"D:\ANDRII\WORK\PROGRAMMING\03_AI\ndb\src\Ndb\bin\Debug\net10.0\netcoredbg\netcoredbg\netcoredbg.exe";

    public static string FindSolutionDir()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new Exception("Could not find solution directory");
    }

    public static string GetNdbDll()
    {
        var solutionDir = FindSolutionDir();
        return Path.Combine(solutionDir, "src", "Ndb", "bin", "Debug", "net10.0", "Ndb.dll");
    }

    public static string GetTestAppDll()
    {
        var solutionDir = FindSolutionDir();
        return Path.Combine(solutionDir, "tests", "TestApp", "bin", "Debug", "net10.0", "TestApp.dll");
    }

    public static async Task<(int ExitCode, string StdOut, string StdErr, JsonElement? Json)> RunNdbAsync(
        string arguments, int timeoutMs = 15000)
    {
        var ndbDll = GetNdbDll();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{ndbDll}\" {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.Environment["NETCOREDBG_PATH"] = NetcoredbgPath;

        using var process = Process.Start(psi)!;
        using var cts = new CancellationTokenSource(timeoutMs);

        // ndb outputs exactly one JSON line — use ReadLineAsync to avoid
        // blocking on EOF which can hang if the daemon inherits the pipe handle
        var stdoutLine = await process.StandardOutput.ReadLineAsync(cts.Token);
        var stdout = stdoutLine ?? "";

        // Drain stderr in background
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        // Wait for the CLI process to exit (the daemon runs separately)
        try { await process.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: false); }

        string stderr = "";
        try { stderr = await stderrTask; }
        catch { }

        JsonElement? json = null;
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            try { json = JsonDocument.Parse(stdout.Trim()).RootElement.Clone(); }
            catch { }
        }

        return (process.ExitCode, stdout.Trim(), stderr.Trim(), json);
    }
}

public class LaunchStatusStopTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Build projects
        var solutionDir = TestHelpers.FindSolutionDir();
        var buildProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{solutionDir}/ndb.slnx\" -v q",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        })!;
        await buildProcess.WaitForExitAsync();
        if (buildProcess.ExitCode != 0)
            throw new Exception("Build failed");
    }

    public async Task DisposeAsync()
    {
        // Ensure cleanup
        await TestHelpers.RunNdbAsync("stop");
        await Task.Delay(500);
    }

    [Fact]
    public async Task Status_WhenNoSession_ReportsInactive()
    {
        await TestHelpers.RunNdbAsync("stop");
        await Task.Delay(500);

        var result = await TestHelpers.RunNdbAsync("status");

        Assert.NotNull(result.Json);
        Assert.True(result.Json.Value.GetProperty("success").GetBoolean());
        Assert.False(result.Json.Value.GetProperty("data").GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task LaunchStatusStop_FullCycle()
    {
        // Clean state
        await TestHelpers.RunNdbAsync("stop");
        await Task.Delay(500);

        // Launch
        var testAppDll = TestHelpers.GetTestAppDll();
        var launchResult = await TestHelpers.RunNdbAsync($"launch \"{testAppDll}\" --stop-on-entry");
        Assert.NotNull(launchResult.Json);
        Assert.True(launchResult.Json.Value.GetProperty("success").GetBoolean(),
            $"Launch failed. stdout: {launchResult.StdOut}, stderr: {launchResult.StdErr}");

        await Task.Delay(1000);

        // Status
        var statusResult = await TestHelpers.RunNdbAsync("status");
        Assert.NotNull(statusResult.Json);
        Assert.True(statusResult.Json.Value.GetProperty("success").GetBoolean());
        Assert.True(statusResult.Json.Value.GetProperty("data").GetProperty("active").GetBoolean());

        // Stop
        var stopResult = await TestHelpers.RunNdbAsync("stop");
        Assert.NotNull(stopResult.Json);
        Assert.True(stopResult.Json.Value.GetProperty("success").GetBoolean(),
            $"Stop failed. stdout: {stopResult.StdOut}, stderr: {stopResult.StdErr}");

        await Task.Delay(500);

        // Verify stopped
        var afterStop = await TestHelpers.RunNdbAsync("status");
        Assert.NotNull(afterStop.Json);
        Assert.False(afterStop.Json.Value.GetProperty("data").GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task Launch_WhenSessionExists_ReturnsError()
    {
        await TestHelpers.RunNdbAsync("stop");
        await Task.Delay(500);

        var testAppDll = TestHelpers.GetTestAppDll();
        var first = await TestHelpers.RunNdbAsync($"launch \"{testAppDll}\" --stop-on-entry");
        Assert.True(first.Json!.Value.GetProperty("success").GetBoolean(),
            $"First launch failed: {first.StdOut} {first.StdErr}");

        await Task.Delay(1000);

        var second = await TestHelpers.RunNdbAsync($"launch \"{testAppDll}\"");
        Assert.NotNull(second.Json);
        Assert.False(second.Json.Value.GetProperty("success").GetBoolean());
        Assert.Contains("already active", second.Json.Value.GetProperty("error").GetString());

        // Cleanup
        await TestHelpers.RunNdbAsync("stop");
    }
}
