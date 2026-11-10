namespace NpcForge.Tests;

// Tone files (ADR-009 section 3): one per tone the roll can give, in every skill folder a run uses.
// A gap must stop the run before anything is paid for, so these tests use no model at all.
[Trait("Category", "Process")]
public class ToneFileTests : IDisposable
{
    private readonly List<string> _cleanup = [];

    public void Dispose()
    {
        foreach (var path in _cleanup)
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
    }

    // The test that fails when a tone is added to the roll without its file, or a file is deleted.
    [Fact]
    public void Every_tone_in_the_schema_has_a_file_in_the_default_skill_folder()
    {
        var fields = Dice.Fields;

        Assert.Equal(["Serious", "Comic", "Heartfelt"], fields.Single(f => f.Name == "tone").Choices);
        Assert.Empty(ToneFiles.Missing(fields, [WriterInput.DefaultSkillDir]));
    }

    [Fact]
    public void A_folder_with_a_tone_file_deleted_is_reported_by_that_files_path()
    {
        var fields = Dice.Fields;
        var dir = CopyOfDefaultSkill();
        File.Delete(ToneFiles.PathOf(dir, "Comic"));

        Assert.Equal([ToneFiles.PathOf(dir, "Comic")], ToneFiles.Missing(fields, [dir, WriterInput.DefaultSkillDir]));
    }

    [Fact]
    public void A_schema_with_no_tone_cannot_be_checked_and_says_so()
    {
        var fields = Dice.Fields.Where(f => f.Name != "tone").ToList();

        var error = Assert.Throws<InvalidOperationException>(() => ToneFiles.Missing(fields, [WriterInput.DefaultSkillDir]));

        Assert.Contains("no 'tone' choice list", error.Message);
    }

    // Program.cs could move the tone check after the greeting or the writer, and a gap would then show
    // only after the GM typed a scene or a paid call. So the app runs as a process, on a copy of its
    // own build with one tone file deleted, as an interview run that must stop before the greeting.
    [Fact]
    public async Task A_run_with_a_tone_file_missing_stops_before_the_interview_and_names_the_file()
    {
        var copy = ConsoleBuildCopy.Make("tonetest", _cleanup);
        File.Delete(Path.Combine(copy, "skills", "npc-writer", "tones", "Heartfelt.md"));

        var run = await ConsoleBuildCopy.RunAsync(copy);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(Path.Combine(copy, "skills", "npc-writer", "tones", "Heartfelt.md"), run.Stderr);
        Assert.DoesNotContain(SceneInterview.Greeting, run.Stderr);
        Assert.DoesNotContain("[loop]", run.Stderr);
    }

    // The positive control: an intact copy reaches the interview, so the test above, and
    // NotGivenFileTests' run with a not-given file missing, stop at their check and not somewhere
    // earlier. Both copy through ConsoleBuildCopy, as this does.
    [Fact]
    public async Task A_copy_with_every_tone_file_reaches_the_interview()
    {
        var run = await ConsoleBuildCopy.RunAsync(ConsoleBuildCopy.Make("tonetest", _cleanup));

        Assert.Equal(0, run.ExitCode);
        Assert.Contains(SceneInterview.Greeting, run.Stderr);
    }

    private string CopyOfDefaultSkill()
    {
        var copy = Path.Combine(Path.GetTempPath(), $"npcforge-skillcopy-{Guid.NewGuid():N}");
        _cleanup.Add(copy);
        ConsoleBuildCopy.CopyDirectory(WriterInput.DefaultSkillDir, copy);
        return copy;
    }
}
