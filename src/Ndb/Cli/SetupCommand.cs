using System;
using System.CommandLine;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ndb.Json;
using Ndb.Models;

namespace Ndb.Cli;

public static class SetupCommand
{
    public static Command Create()
    {
        var command = new Command("setup", "Download and install netcoredbg");

        command.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            // Check if already available
            var envPath = Environment.GetEnvironmentVariable("NETCOREDBG_PATH");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            {
                PrintResponse(NdbResponse.Ok("setup", new { status = "already installed", path = envPath }));
                return;
            }

            var ndbDir = AppContext.BaseDirectory;
            var exeName = OperatingSystem.IsWindows() ? "netcoredbg.exe" : "netcoredbg";
            var localPath = Path.Combine(ndbDir, "netcoredbg", exeName);
            if (File.Exists(localPath))
            {
                PrintResponse(NdbResponse.Ok("setup", new { status = "already installed", path = localPath }));
                return;
            }

            var (osName, archName) = GetPlatformIdentifier();
            if (osName is null)
            {
                PrintResponse(NdbResponse.Fail("setup", $"unsupported platform: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}"));
                return;
            }

            Console.Error.WriteLine($"Downloading netcoredbg for {osName}-{archName}...");

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("ndb/1.0");

                var releaseUrl = "https://api.github.com/repos/Samsung/netcoredbg/releases/latest";
                var releaseJson = await http.GetStringAsync(releaseUrl, ct);
                using var doc = JsonDocument.Parse(releaseJson);
                var assets = doc.RootElement.GetProperty("assets");

                var pattern = $"netcoredbg-{osName}-{archName}";
                string? downloadUrl = null;
                string? assetName = null;

                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString()!;
                    if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        assetName = name;
                        break;
                    }
                }

                if (downloadUrl is null)
                {
                    PrintResponse(NdbResponse.Fail("setup", $"no release found for {pattern}"));
                    return;
                }

                Console.Error.WriteLine($"Found: {assetName}");

                var tempPath = Path.GetTempFileName();
                await using (var stream = await http.GetStreamAsync(downloadUrl, ct))
                await using (var file = File.Create(tempPath))
                {
                    await stream.CopyToAsync(file, ct);
                }

                var extractDir = Path.Combine(ndbDir, "netcoredbg");
                Directory.CreateDirectory(extractDir);

                if (assetName!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(tempPath, extractDir, overwriteFiles: true);
                }
                else if (assetName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    var tar = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "tar",
                        Arguments = $"xzf \"{tempPath}\" -C \"{extractDir}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });
                    tar?.WaitForExit();
                }

                File.Delete(tempPath);

                var finalPath = File.Exists(Path.Combine(extractDir, exeName))
                    ? Path.Combine(extractDir, exeName)
                    : Directory.GetFiles(extractDir, exeName, SearchOption.AllDirectories).FirstOrDefault();

                if (finalPath is null || !File.Exists(finalPath))
                {
                    PrintResponse(NdbResponse.Fail("setup", "extraction succeeded but netcoredbg binary not found"));
                    return;
                }

                if (!OperatingSystem.IsWindows())
                {
                    System.Diagnostics.Process.Start("chmod", $"+x \"{finalPath}\"")?.WaitForExit();
                }

                Console.Error.WriteLine($"Installed to: {finalPath}");
                PrintResponse(NdbResponse.Ok("setup", new { status = "installed", path = finalPath }));
            }
            catch (Exception ex)
            {
                PrintResponse(NdbResponse.Fail("setup", $"download failed: {ex.Message}"));
            }
        });

        return command;
    }

    private static (string? os, string? arch) GetPlatformIdentifier()
    {
        var os = OperatingSystem.IsWindows() ? "win"
               : OperatingSystem.IsLinux() ? "linux"
               : OperatingSystem.IsMacOS() ? "osx"
               : null;

        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        return (os, arch);
    }

    private static void PrintResponse(NdbResponse response)
    {
        Console.WriteLine(JsonSerializer.Serialize(response, NdbJsonContext.Default.NdbResponse));
    }
}
