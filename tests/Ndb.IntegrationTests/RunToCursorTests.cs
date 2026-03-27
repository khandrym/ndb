using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ndb.IntegrationTests;

[Collection("Sequential")]
public class RunToCursorTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var solutionDir = TestHelpers.FindSolutionDir();
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{solutionDir}/ndb.slnx\" -v q",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        var process = System.Diagnostics.Process.Start(psi)!;
        await process.WaitForExitAsync();
    }

    public async Task DisposeAsync()
    {
        await TestHelpers.RunNdbAsync("stop");
        await Task.Delay(500);
    }

    [Fact]
    public async Task RunToCursor_StopsAtTargetLine()
    {
        await TestHelpers.RunNdbAsync("stop");
        await Task.Delay(500);

        var testAppDll = TestHelpers.GetTestAppDll();
        var launch = await TestHelpers.RunNdbAsync($"launch \"{testAppDll}\" --stop-on-entry");
        Assert.True(launch.Json!.Value.GetProperty("success").GetBoolean(),
            $"Launch failed: {launch.StdOut} {launch.StdErr}");
        await Task.Delay(1000);

        var solutionDir = TestHelpers.FindSolutionDir();
        var testAppSource = Path.Combine(solutionDir, "tests", "TestApp", "Program.cs");

        // Run to line 13 (counter = 3;)
        var rtc = await TestHelpers.RunNdbAsync($"exec run-to-cursor \"{testAppSource}\" 13 --timeout 10", timeoutMs: 20000);
        Assert.True(rtc.Json!.Value.GetProperty("success").GetBoolean(),
            $"Run-to-cursor failed: {rtc.StdOut} {rtc.StdErr}");
        Assert.Equal("stopped", rtc.Json.Value.GetProperty("data").GetProperty("status").GetString());

        // Verify no leftover temporary breakpoints
        var list = await TestHelpers.RunNdbAsync("breakpoint list");
        Assert.True(list.Json!.Value.GetProperty("success").GetBoolean());
        var bps = list.Json.Value.GetProperty("data").GetProperty("breakpoints");
        Assert.Equal(0, bps.GetArrayLength());
    }
}
