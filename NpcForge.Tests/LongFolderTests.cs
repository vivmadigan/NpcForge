namespace NpcForge.Tests;

// A build whose path is long. On Windows, once an app's dll path reaches 260 characters (the old
// MAX_PATH), the .NET host hands it AppContext.BaseDirectory as a \\?\ path, and the one-argument
// Path.GetFullPath leaves ".." in such a path as it is. Walking up to the repo with "..\..\..\.."
// then named a folder that does not exist, and the console could not find its server. Nor can the
// server be started by a \\?\ path, as the MCP library starts it through cmd.exe (lesson 015). A
// copy of the repo in a deep folder hit it.
[Trait("Category", "Process")]
public class LongFolderTests : IDisposable
{
    // Measured: a dll path of 259 characters gets a plain folder, one of 260 gets \\?\. The copy goes
    // 20 past it, so the test still reaches a \\?\ folder if the host's rule moves by one.
    private const int MaxPath = 260;

    private readonly List<string> _cleanup = [];

    public void Dispose()
    {
        foreach (var path in _cleanup)
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
    }

    // The replay provider and the dead proxy make it free; no answer flag makes it an interview
    // run, which reaches the greeting only after the server has answered.
    [Fact]
    public async Task A_console_build_in_a_long_folder_still_finds_its_server()
    {
        var copy = CopyOfConsoleBuildInALongFolder();
        var fixture = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "replay.jsonl");
        var save = Path.Combine(Path.GetTempPath(), $"npcforge-longfolder-{Guid.NewGuid():N}.json");
        try
        {
            var run = await ConsoleProcess.RunAsync(
                ["--provider", "replay", "--recording", fixture],
                offline: true,
                environment: new() { ["NPCFORGE_CHARACTERS"] = save },
                consoleDll: Path.Combine(copy, "NpcForge.Console.dll"));

            // The console's stderr as the message, so a failure says why the run stopped.
            Assert.True(run.ExitCode == 0, run.Stderr);
            Assert.Contains(SceneInterview.Greeting, run.Stderr);
        }
        finally { File.Delete(save); }
    }

    // A sibling of net10.0, so the repo is still four folders up, with a name padded until the dll
    // path is past MaxPath from any checkout, short or deep.
    private string CopyOfConsoleBuildInALongFolder()
    {
        var bin = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Console", "bin", ConsoleProcess.BuildConfiguration);
        var name = $"net10.0-longfolder-{Guid.NewGuid():N}";
        while (Path.Combine(bin, name, "NpcForge.Console.dll").Length < MaxPath + 20)
            name += "x";
        var copy = Path.Combine(bin, name);
        _cleanup.Add(copy);
        ConsoleBuildCopy.CopyDirectory(Path.Combine(bin, "net10.0"), copy);
        return copy;
    }
}
