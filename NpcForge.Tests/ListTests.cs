namespace NpcForge.Tests;

// ADR-013, decision 5, through the whole app as a process, with the same free stops as LoadTests:
// NPCFORGE_OFFLINE=1, the dead proxy, a temp save file, and the default provider (openai), which the
// offline stop refuses, so a run that lists never built a model client.
[Trait("Category", "Process")]
public class ListTests
{
    private const string OfflineMessage = "NPCFORGE_OFFLINE=1 is set";

    private static Dictionary<string, string?> Saves(string file) => new() { ["NPCFORGE_CHARACTERS"] = file };

    [Fact]
    public async Task List_shows_every_save_with_its_number_name_occupation_and_setting()
    {
        var file = SavedFile.WriteTemp();
        try
        {
            var run = await ConsoleProcess.RunAsync(["--list"], offline: true, environment: Saves(file));

            Assert.Equal(0, run.ExitCode);
            Assert.DoesNotContain(OfflineMessage, run.Stderr);

            // stdout holds the rows and nothing else, in number order (10 sorts before 8 as text).
            var rows = run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Equal(3, rows.Length);

            // The old save has no name; the one with no occupation shows a gap, not a made-up word.
            AssertRow(rows[0], "8", "(no name)", "goblin baker", "tiny bakery");
            AssertRow(rows[1], "9", "Hargrim Rockhammer", "innkeeper", "an inn in a major city");
            AssertRow(rows[2], "10", "Tam Reed", null, "a river barge");

            // A list reads the name and the brief, never the text (ADR-013, decision 4).
            Assert.DoesNotContain(SavedFile.TextMarker, run.Stdout + run.Stderr);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task List_of_an_empty_file_says_so_on_stderr_and_exits_0()
    {
        var file = Path.Combine(Path.GetTempPath(), $"npcforge-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, "{}");
        try
        {
            var run = await ConsoleProcess.RunAsync(["--list"], offline: true, environment: Saves(file));

            Assert.Equal(0, run.ExitCode);
            Assert.Contains("[app] nothing saved yet", run.Stderr);
            Assert.Equal("", run.Stdout.Trim());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task List_of_a_missing_file_says_so_on_stderr_and_exits_0()
    {
        var file = Path.Combine(Path.GetTempPath(), $"npcforge-test-{Guid.NewGuid():N}.json");

        var run = await ConsoleProcess.RunAsync(["--list"], offline: true, environment: Saves(file));

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("[app] nothing saved yet", run.Stderr);
        Assert.Equal("", run.Stdout.Trim());
        Assert.False(File.Exists(file), "listing writes nothing");
    }

    // A row is its fields in order, split on runs of two or more spaces so a name or a setting may
    // hold single spaces. The test pins the order and the content, not the padding.
    private static void AssertRow(string row, string number, string name, string? occupation, string setting)
    {
        var cells = System.Text.RegularExpressions.Regex.Split(row, @"\s{2,}");
        Assert.Equal([number, name, occupation ?? "-", setting], cells);
    }
}
