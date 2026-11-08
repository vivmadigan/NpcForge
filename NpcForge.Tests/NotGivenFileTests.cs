namespace NpcForge.Tests;

// Not-given files (ADR-010, decision 5): one per field the scene may leave out, in the default skill
// folder. A gap must stop an interview run before anything is paid for, so these tests use no model.
[Trait("Category", "Process")]
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

    // The app as a process, on a copy of its own build with the file deleted. Its positive control is
    // ToneFileTests' run of an intact copy, made through ConsoleBuildCopy as this one is.
    [Fact]
    public async Task A_run_with_a_not_given_file_missing_stops_before_the_interview_and_names_the_file()
    {
        var copy = ConsoleBuildCopy.Make("notgiventest", _cleanup);
        var file = Path.Combine(copy, "skills", "npc-writer", "not-given", "playersWant.md");
        File.Delete(file);

        var run = await ConsoleBuildCopy.RunAsync(copy);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(file, run.Stderr);
        Assert.DoesNotContain(SceneInterview.Greeting, run.Stderr);
        Assert.DoesNotContain("[loop]", run.Stderr);
    }
}