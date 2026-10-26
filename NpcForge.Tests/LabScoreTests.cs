namespace NpcForge.Tests;

// --score reads a finished blind lab and calls no model. In process through Lab.ScoreAsync for the
// dialogue and the sheet, and as the whole app for "needs no key and no server".
public class LabScoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"npcforge-score-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    // A finished blind lab of six, made by the same pure function the lab uses.
    private Dictionary<string, string> BlindLab()
    {
        var entries = new[] { "gentle", "terse", "wry" }
            .SelectMany(label => new[] { 1, 2 }.Select(take =>
                new LabEntry(label, take, "[skill] x", $"{label} {take} text", null)))
            .ToList();
        var (sheet, key, data) = LabSheet.Blind("""{"setting":"a farm"}""", entries, 6, new Random(9));
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "blind.md"), sheet);
        File.WriteAllText(Path.Combine(_folder, "blind.json"), data);
        File.WriteAllText(Path.Combine(_folder, "key.json"), key);
        return new Dictionary<string, string>(LabSheet.ReadKey(key));
    }

    // The guesses a GM would type: every letter right but the last.
    private static string Guesses(Dictionary<string, string> key) =>
        string.Join('\n', key.OrderBy(p => p.Key).Select((p, i) => i == key.Count - 1 ? "nobody" : p.Value.ToUpperInvariant())) + "\n";

    [Fact]
    public async Task Score_asks_for_a_guess_per_letter_prints_five_of_six_and_writes_scored_md()
    {
        var key = BlindLab();
        var dialogue = new StringWriter();
        var result = new StringWriter();

        await Lab.ScoreAsync(_folder, new StringReader(Guesses(key)), dialogue, result, CancellationToken.None);

        Assert.Equal("5 of 6 right", result.ToString().Trim());
        foreach (var letter in key.Keys)
            Assert.Contains($"[score] Who wrote {letter}? ", dialogue.ToString());
        Assert.Contains("The labels are: gentle, terse, wry.", dialogue.ToString());

        var scored = File.ReadAllText(Path.Combine(_folder, "scored.md"));
        Assert.Contains("Your guess: nobody. Wrong.", scored);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(scored, @"\. Right\.").Count);
        Assert.Contains("gentle 1 text", scored);
    }

    [Fact]
    public async Task Score_at_end_of_input_counts_the_unguessed_as_wrong()
    {
        BlindLab();
        var result = new StringWriter();

        await Lab.ScoreAsync(_folder, new StringReader(""), new StringWriter(), result, CancellationToken.None);

        Assert.Equal("0 of 6 right", result.ToString().Trim());
    }

    [Fact]
    public async Task Score_of_a_folder_that_is_not_a_blind_lab_says_what_it_needs()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "sheet.md"), "a side sheet");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Lab.ScoreAsync(_folder, new StringReader(""), new StringWriter(), new StringWriter(), CancellationToken.None));

        Assert.Contains("must hold blind.json and key.json", error.Message);
    }

    // The whole app: no API key (the child has none in its user secrets here and a dead proxy), no
    // server, and the offline stop set. The only things it may do are read the folder and write
    // scored.md.
    [Fact]
    public async Task The_app_scores_with_no_key_and_no_server()
    {
        var key = BlindLab();

        var run = await ConsoleProcess.RunAsync(["--score", _folder], offline: true, input: Guesses(key));

        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Equal("5 of 6 right", run.Stdout.Trim());
        Assert.True(File.Exists(Path.Combine(_folder, "scored.md")));
        // No server was started, no key was read and no client was built.
        Assert.DoesNotContain("[server]", run.Stderr);
        Assert.DoesNotContain("Missing user secret", run.Stderr);
        Assert.DoesNotContain("NPCFORGE_OFFLINE=1 is set", run.Stderr);
    }
}
