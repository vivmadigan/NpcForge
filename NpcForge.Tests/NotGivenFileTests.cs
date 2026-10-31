namespace NpcForge.Tests;

// Not-given files (ADR-010, decision 5): one per field the scene may leave out, in the default skill
// folder. A gap must stop an interview run before anything is paid for, so these tests use no model.
public class NotGivenFileTests : IDisposable
{
    private readonly List<string> _cleanup = [];

    public void Dispose()
    {
        foreach (var path in _cleanup)
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
    }

    [Fact]
    public async Task Every_field_the_scene_may_leave_out_has_a_file_in_the_default_skill_folder()
    {
        var save = Path.Combine(Path.GetTempPath(), $"npcforge-notgiven-{Guid.NewGuid():N}.json");
        await using var tools = new McpToolSource(save, seed: 1);
        await tools.ConnectAsync(CancellationToken.None);
        var fields = RollField.ListFrom(tools.SchemaOf("roll_character"));

        Assert.Contains(fields, f => f.WhenMissing == WhenMissing.NotGiven);
        Assert.Empty(NotGivenFiles.Missing(fields, [WriterInput.DefaultSkillDir]));
    }

    // The app as a process, on a copy of its own build with the file deleted. The replay provider and
    // the dead proxy make it free; no answer flag makes it an interview run, which would print the
    // greeting first if the check came after it.
    [Fact]
    public async Task A_run_with_a_not_given_file_missing_stops_before_the_interview_and_names_the_file()
    {
        var copy = CopyOfConsoleBuild();
        var file = Path.Combine(copy, "skills", "npc-writer", "not-given", "playersWant.md");
        File.Delete(file);

        var run = await RunCopy(copy);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(file, run.Stderr);
        Assert.DoesNotContain(SceneInterview.Greeting, run.Stderr);
        Assert.DoesNotContain("[loop]", run.Stderr);
    }

    // The positive control: the same copy with the file in place reaches the interview, so the test
    // above stops at the not-given check and not somewhere earlier.
    [Fact]
    public async Task A_copy_with_every_not_given_file_reaches_the_interview()
    {
        var run = await RunCopy(CopyOfConsoleBuild());

        Assert.Equal(0, run.ExitCode);
        Assert.Contains(SceneInterview.Greeting, run.Stderr);
    }

    private static async Task<ConsoleProcess.Result> RunCopy(string copy)
    {
        var fixture = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "replay.jsonl");
        var save = Path.Combine(Path.GetTempPath(), $"npcforge-notgivencopy-{Guid.NewGuid():N}.json");
        try
        {
            return await ConsoleProcess.RunAsync(
                ["--provider", "replay", "--recording", fixture],
                offline: true,
                environment: new() { ["NPCFORGE_CHARACTERS"] = save },
                consoleDll: Path.Combine(copy, "NpcForge.Console.dll"));
        }
        finally { File.Delete(save); }
    }

    // A sibling of the console's own bin folder, so the copy still finds the server four folders up
    // from itself, as McpToolSource looks (the same copy ToneFileTests makes).
    private string CopyOfConsoleBuild()
    {
        var bin = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Console", "bin", ConsoleProcess.BuildConfiguration);
        var copy = Path.Combine(bin, $"net10.0-notgiventest-{Guid.NewGuid():N}");
        _cleanup.Add(copy);
        CopyDirectory(Path.Combine(bin, "net10.0"), copy);
        return copy;
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(from))
            CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}