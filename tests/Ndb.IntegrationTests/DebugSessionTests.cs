using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ndb.IntegrationTests;

[Collection("Sequential")]
public class DebugSessionTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Build entire solution
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
        await TestHelpers.RunNdbAsync("stop");
        await Task.Delay(500);
    }

    private async Task LaunchWithStopOnEntryAsync()
    {
        await TestHelpers.RunNdbAsync("stop");
        await Task.Delay(500);

        var testAppDll = TestHelpers.GetTestAppDll();
        var launchResult = await TestHelpers.RunNdbAsync($"launch \"{testAppDll}\" --stop-on-entry");
        Assert.NotNull(launchResult.Json);
        Assert.True(launchResult.Json.Value.GetProperty("success").GetBoolean(),
            $"Launch failed. stdout: {launchResult.StdOut}, stderr: {launchResult.StdErr}");

        await Task.Delay(1000);
    }

    [Fact]
    public async Task LaunchWithStopOnEntry_InspectStacktrace()
    {
        await LaunchWithStopOnEntryAsync();

        var result = await TestHelpers.RunNdbAsync("inspect stacktrace");

        Assert.NotNull(result.Json);
        Assert.True(result.Json.Value.GetProperty("success").GetBoolean(),
            $"inspect stacktrace failed. stdout: {result.StdOut}, stderr: {result.StdErr}");

        var frames = result.Json.Value.GetProperty("data").GetProperty("frames");
        Assert.Equal(JsonValueKind.Array, frames.ValueKind);
        Assert.True(frames.GetArrayLength() > 0,
            $"Expected at least one frame, got: {result.StdOut}");
    }

    [Fact]
    public async Task LaunchWithStopOnEntry_InspectThreads()
    {
        await LaunchWithStopOnEntryAsync();

        var result = await TestHelpers.RunNdbAsync("inspect threads");

        Assert.NotNull(result.Json);
        Assert.True(result.Json.Value.GetProperty("success").GetBoolean(),
            $"inspect threads failed. stdout: {result.StdOut}, stderr: {result.StdErr}");

        var threads = result.Json.Value.GetProperty("data").GetProperty("threads");
        Assert.Equal(JsonValueKind.Array, threads.ValueKind);
        Assert.True(threads.GetArrayLength() > 0,
            $"Expected at least one thread, got: {result.StdOut}");
    }

    [Fact]
    public async Task LaunchWithStopOnEntry_StepOver()
    {
        await LaunchWithStopOnEntryAsync();

        // Use 30s timeout for --wait commands (--timeout 10 means 10s DAP wait + overhead)
        var result = await TestHelpers.RunNdbAsync("exec step-over --wait --timeout 10", timeoutMs: 30000);

        Assert.NotNull(result.Json);
        Assert.True(result.Json.Value.GetProperty("success").GetBoolean(),
            $"exec step-over failed. stdout: {result.StdOut}, stderr: {result.StdErr}");

        var data = result.Json.Value.GetProperty("data");
        var status = data.GetProperty("status").GetString();
        Assert.Equal("stopped", status);
    }

    [Fact]
    public async Task LaunchWithStopOnEntry_Evaluate()
    {
        await LaunchWithStopOnEntryAsync();

        var result = await TestHelpers.RunNdbAsync("inspect evaluate \"1+1\"");

        Assert.NotNull(result.Json);
        Assert.True(result.Json.Value.GetProperty("success").GetBoolean(),
            $"inspect evaluate failed. stdout: {result.StdOut}, stderr: {result.StdErr}");

        var evalResult = result.Json.Value.GetProperty("data").GetProperty("result").GetString();
        Assert.Equal("2", evalResult);
    }

    [Fact]
    public async Task BreakpointSet_ContinueWait_HitsBreakpoint()
    {
        await LaunchWithStopOnEntryAsync();

        var solutionDir = TestHelpers.FindSolutionDir();
        var programCsPath = Path.Combine(solutionDir, "tests", "TestApp", "Program.cs");

        // Set breakpoint on line 12 (counter = 2)
        var bpResult = await TestHelpers.RunNdbAsync($"breakpoint set \"{programCsPath}\" 12");
        Assert.NotNull(bpResult.Json);
        Assert.True(bpResult.Json.Value.GetProperty("success").GetBoolean(),
            $"breakpoint set failed. stdout: {bpResult.StdOut}, stderr: {bpResult.StdErr}");

        // Continue and wait for hit — use 30s timeout
        var contResult = await TestHelpers.RunNdbAsync("exec continue --wait --timeout 10", timeoutMs: 30000);
        Assert.NotNull(contResult.Json);
        Assert.True(contResult.Json.Value.GetProperty("success").GetBoolean(),
            $"exec continue failed. stdout: {contResult.StdOut}, stderr: {contResult.StdErr}");

        var data = contResult.Json.Value.GetProperty("data");
        var status = data.GetProperty("status").GetString();
        Assert.Equal("stopped", status);
    }
}
