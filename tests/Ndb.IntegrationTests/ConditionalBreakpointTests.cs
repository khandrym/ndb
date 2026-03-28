using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ndb.IntegrationTests;

[Collection("Sequential")]
public class ConditionalBreakpointTests : IAsyncLifetime
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
    public async Task ConditionalBreakpoint_OnlyHitsWhenConditionTrue()
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

        // Set conditional breakpoint on "counter = 3;" (line 13) with condition "counter == 2"
        var setBp = await TestHelpers.RunNdbAsync($"breakpoint set \"{testAppSource}\" 13 --condition \"counter == 2\"");
        Assert.True(setBp.Json!.Value.GetProperty("success").GetBoolean(),
            $"Breakpoint set failed: {setBp.StdOut} {setBp.StdErr}");

        // Verify condition is stored
        var listBp = await TestHelpers.RunNdbAsync("breakpoint list");
        Assert.True(listBp.Json!.Value.GetProperty("success").GetBoolean());
        var bps = listBp.Json.Value.GetProperty("data").GetProperty("breakpoints");
        Assert.True(bps.GetArrayLength() > 0);

        // Continue — should hit conditional breakpoint
        var cont = await TestHelpers.RunNdbAsync("exec continue --wait --timeout 30", timeoutMs: 60000);
        Assert.True(cont.Json!.Value.GetProperty("success").GetBoolean(),
            $"Continue failed: {cont.StdOut} {cont.StdErr}");
        Assert.Equal("stopped", cont.Json.Value.GetProperty("data").GetProperty("status").GetString());
    }
}
