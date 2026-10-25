using System.Diagnostics;               // Process, ProcessStartInfo

namespace NpcForge.Tests;

// The refusal in RunOptions.Parse, through the whole app as a process: a typo'd flag next to the
// default paid provider must stop at the parser. Both independent stops are set, NPCFORGE_OFFLINE=1
// and the dead proxy, so a parser that stopped refusing would fail for free. The test asserts the
// parser's message and not the offline one, which is what shows the parser did the stopping
// (lesson 004).
public class FlagRefusalTests
{
    [Theory]
    [InlineData(new[] { "--provider", "openai", "--setings", "farm" }, "Unknown flag --setings.")]
    [InlineData(new[] { "--provider", "openai", "--setting" }, "--setting needs a value after it.")]
    public async Task The_app_stops_at_the_parser_before_any_client_is_built(string[] flags, string message)
    {
        var run = await RunConsole(flags);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(message, run.Stderr);
        Assert.DoesNotContain("NPCFORGE_OFFLINE=1 is set", run.Stderr);
        Assert.DoesNotContain("Missing user secret", run.Stderr);
        Assert.DoesNotContain("[loop]", run.Stderr);
    }

    private static async Task<(int ExitCode, string Stderr)> RunConsole(string[] flags)
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { "run", "--project", Path.Combine(repo, "NpcForge.Console"), "--no-build", "--no-launch-profile", "--" })
            start.ArgumentList.Add(arg);
        foreach (var flag in flags)
            start.ArgumentList.Add(flag);

        start.Environment[ModelClients.OfflineVariable] = "1";
        start.Environment["HTTPS_PROXY"] = "http://127.0.0.1:9";
        start.Environment["HTTP_PROXY"] = "http://127.0.0.1:9";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        await stdout;
        return (process.ExitCode, await stderr);
    }
}
