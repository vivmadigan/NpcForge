namespace NpcForge.Tests;

// A build whose path is long. On Windows, once an app's dll path reaches 260 characters (the old
// MAX_PATH), the .NET host hands it AppContext.BaseDirectory as a \\?\ path, and the one-argument
// Path.GetFullPath leaves ".." in such a path as it is. Walking up "..\..\.." to the default save file
// then names a file that does not exist, and --list says nothing is saved when the GM has saves
// (lesson 015). The same walk up found the MCP server until it retired; a copy of the repo in a deep
// folder hit it.
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

    // No NPCFORGE_CHARACTERS, so the copy opens its default save file. The copy sits three folders under
    // a temp folder that holds a save file of its own, as the console's bin folder sits three under
    // NpcForge.Console, so the walk up reaches that file and never the owner's. --list builds no model
    // client, and the offline stop and the dead proxy keep it free.
    [Fact]
    public async Task A_console_build_in_a_long_folder_still_finds_its_default_save_file()
    {
        var (root, copy) = CopyOfConsoleBuildInALongFolder();
        var saves = SavedFile.WriteTemp();
        File.Move(saves, Path.Combine(root, "characters.json"));

        var run = await ConsoleProcess.RunAsync(
            ["--list"],
            offline: true,
            environment: new() { ["NPCFORGE_CHARACTERS"] = null },
            consoleDll: Path.Combine(copy, "NpcForge.Console.dll"));

        // The console's stderr as the message, so a failure says why the run stopped.
        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.DoesNotContain("[app] nothing saved yet", run.Stderr);
        Assert.Equal(3, run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length);
    }

    // A copy of the console's build at <root>\bin\<configuration>\<name>, with a name padded until the
    // dll path is past MaxPath from any checkout, short or deep.
    private (string Root, string Copy) CopyOfConsoleBuildInALongFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"npcforge-longfolder-{Guid.NewGuid():N}");
        _cleanup.Add(root);
        var bin = Path.Combine(root, "bin", ConsoleProcess.BuildConfiguration);
        var name = "net10.0-longfolder-";
        while (Path.Combine(bin, name, "NpcForge.Console.dll").Length < MaxPath + 20)
            name += "x";
        var copy = Path.Combine(bin, name);
        ConsoleBuildCopy.CopyDirectory(
            Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Console", "bin", ConsoleProcess.BuildConfiguration, "net10.0"), copy);
        return (root, copy);
    }
}
