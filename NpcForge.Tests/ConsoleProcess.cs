using System.Diagnostics;               // Process, ProcessStartInfo
using System.Reflection;                // AssemblyConfigurationAttribute

namespace NpcForge.Tests;

// The one way a test starts the whole app as a process. It lives here so the rules that keep a
// test run free and honest are written once and cannot be forgotten in a fourth copy.
internal static class ConsoleProcess
{
    public sealed record Result(int ExitCode, string Stdout, string Stderr);

    // Four folders up from a test's bin is the repo, as McpToolSource finds the server.
    public static string RepoRoot { get; } =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    // The SDK stamps the test assembly with the Configuration it was built under, so this is what
    // `dotnet test -c Release` set, not a guess from a path. The console is a project reference, so
    // it was built in the same one. Without it, `dotnet run --no-build` falls back to Debug and
    // runs a different build from the one under test (ROADMAP epic "Harden the safety net").
    public static string BuildConfiguration { get; } =
        typeof(ConsoleProcess).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
        ?? throw new InvalidOperationException("The test assembly carries no build configuration.");

    // Runs the console in BuildConfiguration. `offline` sets NPCFORGE_OFFLINE=1, the stop in the
    // paid model clients. `environment` adds or overrides variables for the child only.
    public static async Task<Result> RunAsync(
        string[] flags, bool offline = false, Dictionary<string, string?>? environment = null)
    {
        // --no-build runs whatever is in bin, so a missing or old build would run old code and
        // pass or fail for the wrong reason. The console starts the server in this same
        // configuration, so both are checked.
        RequireFreshBuild("NpcForge.Console");
        RequireFreshBuild("NpcForge.Server");

        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
        {
            "run", "--project", Path.Combine(RepoRoot, "NpcForge.Console"),
            "--no-build", "-c", BuildConfiguration, "--no-launch-profile", "--",
        })
            start.ArgumentList.Add(arg);
        foreach (var flag in flags)
            start.ArgumentList.Add(flag);

        // Free by construction, not by one flag. The child console can read the owner's real API
        // keys from its user secrets. A proxy that nothing listens on makes any paid call fail at
        // the proxy, and the telemetry opt-out stops the dotnet CLI itself from sending anything
        // through it. Always set: no test here needs the network.
        start.Environment["HTTPS_PROXY"] = "http://127.0.0.1:9";
        start.Environment["HTTP_PROXY"] = "http://127.0.0.1:9";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        if (offline)
            start.Environment[ModelClients.OfflineVariable] = "1";
        // A null value removes the variable, so a test can run as if the owner's shell never set it.
        foreach (var (name, value) in environment ?? [])
            start.Environment[name] = value;

        using var process = Process.Start(start)!;

        // Read both streams while it runs. Left unread, the [server] and [loop] lines fill the
        // pipe and the app stops, waiting for someone to read them.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        // A minute is far more than any of these runs needs. A hang fails the test instead of the
        // test run never ending.
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

        return new Result(process.ExitCode, await stdout, await stderr);
    }

    // Unlike the verify script this includes the console's skill, which is product and is copied
    // to bin.
    private static void RequireFreshBuild(string name)
    {
        var project = Path.Combine(RepoRoot, name);
        var dll = Path.Combine(project, "bin", BuildConfiguration, "net10.0", $"{name}.dll");
        Assert.True(File.Exists(dll),
            $"The {BuildConfiguration} build of {name} is missing ({dll}). Run `dotnet build -c {BuildConfiguration}`.");

        // characters.json is the owner's saves, not code: a save, or a test putting it back, must
        // not make the build look old.
        var newest = Directory.EnumerateFiles(project, "*", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file) != "characters.json")
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
        Assert.True(File.GetLastWriteTimeUtc(dll) >= File.GetLastWriteTimeUtc(newest),
            $"The {BuildConfiguration} build of {name} is stale: {Path.GetRelativePath(RepoRoot, newest)} is newer than the build. Run `dotnet build -c {BuildConfiguration}`.");
    }
}
