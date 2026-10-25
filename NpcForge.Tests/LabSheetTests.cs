namespace NpcForge.Tests;

// The lab's sheets are pure: entries in, text out. No model, no server, no files.
public class LabSheetTests
{
    private const string Brief = """{"setting":"a farm"}""";

    private static LabEntry Wrote(string label, int take, string text) =>
        new(label, take, $"[skill] /skills/{label}/SKILL.md (10 chars, abcd1234)", text, null);

    private static LabEntry Failed(string label, int take, string reason) =>
        new(label, take, $"[skill] /skills/{label}/SKILL.md (10 chars, abcd1234)", null, reason);

    private static readonly LabEntry[] Six =
    [
        Wrote("gentle", 1, "G1 text"), Wrote("gentle", 2, "G2 text"),
        Wrote("terse", 1, "T1 text"), Wrote("terse", 2, "T2 text"),
        Wrote("wry", 1, "W1 text"), Wrote("wry", 2, "W2 text"),
    ];

    [Fact]
    public void Side_by_side_shows_the_brief_once_and_every_character_under_its_label_and_take()
    {
        var sheet = LabSheet.SideBySide("skill", Brief, Six, planned: 6);

        Assert.Equal(1, sheet.Split(Brief).Length - 1);
        foreach (var entry in Six)
        {
            var at = sheet.IndexOf($"## {entry.Label}, take {entry.Take}", StringComparison.Ordinal);
            Assert.True(at >= 0, $"{entry.Label} take {entry.Take} is on the sheet");
            // Its skill line and its text follow its heading, in that order.
            var section = sheet[at..];
            Assert.True(section.IndexOf(entry.Skill, StringComparison.Ordinal) < section.IndexOf(entry.Text!, StringComparison.Ordinal));
        }
        Assert.DoesNotContain("stopped", sheet);
    }

    [Fact]
    public void A_failed_variant_stays_on_the_side_sheet_with_its_reason()
    {
        var sheet = LabSheet.SideBySide("temperature",
            Brief, [Wrote("0.2", 1, "cool"), Failed("0.9", 1, "HTTP 400: temperature is not supported")], planned: 2);

        Assert.Contains("## 0.9, take 1", sheet);
        Assert.Contains("**Failed.** HTTP 400: temperature is not supported", sheet);
        Assert.Contains("cool", sheet);
    }

    [Fact]
    public void Fewer_entries_than_planned_says_where_the_lab_stopped()
    {
        var sheet = LabSheet.SideBySide("skill", Brief, Six[..4], planned: 6);

        Assert.Contains("stopped at the spending cap after 4 of 6", sheet);
    }

    [Fact]
    public void Blind_letters_every_written_character_and_the_key_names_each_ones_label()
    {
        var (sheet, keyJson) = LabSheet.Blind(Brief, Six, planned: 6, new Random(7));
        var key = LabSheet.ReadKey(keyJson);

        Assert.Equal(["A", "B", "C", "D", "E", "F"], key.Keys);
        foreach (var (letter, label) in key)
        {
            // Each letter's text is the text of a character written under that label.
            var section = sheet[sheet.IndexOf($"## Character {letter}", StringComparison.Ordinal)..];
            var text = section.Split('\n')[2].Trim();
            Assert.Contains(Six, entry => entry.Label == label && entry.Text == text);
        }
        // Every written character appears once.
        foreach (var entry in Six)
            Assert.Equal(1, sheet.Split(entry.Text!).Length - 1);
    }

    [Fact]
    public void A_blind_sheet_carries_no_label_no_skill_line_and_no_failure_reason()
    {
        var entries = Six.Append(Failed("wry", 3, "HTTP 400 on wry")).ToList();

        var (sheet, _) = LabSheet.Blind(Brief, entries, planned: 7, new Random(1));

        foreach (var secret in new[] { "gentle", "terse", "wry", "/skills/", "[skill]", "abcd1234", "HTTP 400", "take" })
            Assert.DoesNotContain(secret, sheet);
        // The failure is counted, not described.
        Assert.Contains("1 character failed to write; the reasons are in key.json", sheet);
    }

    [Fact]
    public void The_same_seed_gives_the_same_letters_and_a_different_seed_a_different_order()
    {
        var one = LabSheet.Blind(Brief, Six, 6, new Random(3));
        var again = LabSheet.Blind(Brief, Six, 6, new Random(3));
        var others = Enumerable.Range(4, 20).Select(seed => LabSheet.Blind(Brief, Six, 6, new Random(seed)).Key);

        Assert.Equal(one, again);
        Assert.Contains(others, key => key != one.Key);
    }

    [Fact]
    public void Shuffle_is_a_permutation_decided_by_the_random()
    {
        var items = Enumerable.Range(0, 10).ToList();

        var shuffled = LabSheet.Shuffle(items, new Random(5));

        Assert.Equal(items, shuffled.Order());
        Assert.NotEqual(items, shuffled);
        Assert.Equal(shuffled, LabSheet.Shuffle(items, new Random(5)));
    }

    // The score the ADR names: 5 of 6 from piped guesses.
    [Fact]
    public void Score_gives_five_of_six_and_writes_each_label_and_guess_beside_its_letter()
    {
        var (blind, keyJson) = LabSheet.Blind(Brief, Six, 6, new Random(2));
        var key = LabSheet.ReadKey(keyJson);
        // Every guess right but the last, typed in a different case, and the last one wrong.
        var guesses = key.ToDictionary(pair => pair.Key, pair => pair.Value.ToUpperInvariant());
        guesses["F"] = key["F"] == "wry" ? "terse" : "wry";

        var (right, total, scored) = LabSheet.Score(blind, key, guesses);

        Assert.Equal((5, 6), (right, total));
        Assert.Contains($"Label: {key["A"]}. Your guess: {key["A"].ToUpperInvariant()}. Right.", scored);
        Assert.Contains($"Label: {key["F"]}. Your guess: {guesses["F"]}. Wrong.", scored);
        // The text of the characters is still there.
        Assert.All(Six, entry => Assert.Contains(entry.Text!, scored));
    }

    [Fact]
    public void A_letter_with_no_guess_is_wrong()
    {
        var (blind, keyJson) = LabSheet.Blind(Brief, Six[..2], 2, new Random(2));

        var (right, total, scored) = LabSheet.Score(blind, LabSheet.ReadKey(keyJson), new Dictionary<string, string>());

        Assert.Equal((0, 2), (right, total));
        Assert.Contains("Your guess: none. Wrong.", scored);
    }
}
