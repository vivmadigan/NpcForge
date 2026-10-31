using System.Text.Json;

namespace NpcForge.Tests;

// ADR-009 decision 2: "unknown" exists only on the screen. SavedView.Unknown lists what a saved
// brief lacks, read from the roll schema so a field added later shows up with no edit. Pure, with
// one process test that drives --load on an old save.
public class SavedViewTests
{
    // An entry from before the five fields: the brief has the eleven, nothing more.
    private const string OldBrief = """
        "setting": "an inn in a major city", "playersWant": "the name of a fence", "difficulty": "Wall",
        "occupation": "innkeeper", "characterWants": "to be left alone", "obstacle": "Cant",
        "attitude": "distrustful", "mannerism": "talks with their hands", "weakness": "is easily flattered",
        "willNotDiscuss": "their age", "wrongAbout": "believes they are an excellent liar"
        """;

    private const string NewFields = """
        , "tone": "Comic", "race": "dwarf", "nameLetter": "K", "nameSyllables": "2", "familyLetter": "V"
        """;

    // A name as the server saves it.
    private const string Kessa = """ "name": { "syllables": ["Kes", "sa"], "family": "Vale" }, """;

    private static JsonElement Entry(string briefFields, string name = "")
    {
        using var entry = JsonDocument.Parse($$"""{ "brief": { {{briefFields}} },{{name}} "text": "A character." }""");
        return entry.RootElement.Clone();
    }

    [Fact]
    public void An_old_save_lacks_the_five_fields_and_a_name_and_they_are_listed_in_schema_order_then_name()
    {
        var unknown = SavedView.Unknown(SceneDraftTests.Fields, Entry(OldBrief));

        Assert.Equal(["tone", "race", "nameLetter", "nameSyllables", "familyLetter", "name"], unknown);
    }

    [Fact]
    public void A_save_with_every_field_and_a_name_has_nothing_unknown()
    {
        Assert.Empty(SavedView.Unknown(SceneDraftTests.Fields, Entry(OldBrief + NewFields, Kessa)));
    }

    [Fact]
    public void A_save_with_every_brief_field_and_no_name_lists_only_name()
    {
        Assert.Equal(["name"], SavedView.Unknown(SceneDraftTests.Fields, Entry(OldBrief + NewFields)));
    }

    [Fact]
    public void The_saved_name_is_read_as_the_namer_split_it()
    {
        var name = SavedView.NameOf(Entry(OldBrief + NewFields, Kessa));

        Assert.Equal("Kessa Vale", name!.Full);
        Assert.Equal(["Kes", "sa"], name.Syllables);
        Assert.Null(SavedView.NameOf(Entry(OldBrief)));
    }

    // Read from the schema it is handed, not from a list of its own: a field the console has never
    // heard of, added to the brief later, shows as unknown with no edit here.
    [Fact]
    public void A_field_added_to_the_schema_later_is_unknown_with_no_edit()
    {
        RollField[] fields = [.. SceneDraftTests.Fields, new("pet", "A pet", null, WhenMissing.Roll)];

        var unknown = SavedView.Unknown(fields, Entry(OldBrief + NewFields, Kessa));

        Assert.Equal(["pet"], unknown);
    }

    // The process: --load on an old save prints the brief line exactly as saved, then the unknown line,
    // then the text. A save with every field prints no unknown line, which is the positive control: the
    // line is not simply always there. The replay provider and the dead proxy keep it free.
    [Fact]
    public async Task Load_prints_the_unknown_line_after_the_brief_line_for_an_old_save()
    {
        var old = await LoadOf(OldBrief);
        var full = await LoadOf(OldBrief + NewFields, Kessa);

        Assert.True(old.ExitCode == 0, old.Stderr);
        var lines = old.Stderr.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var brief = lines.FindIndex(l => l.StartsWith("[app] brief {"));
        Assert.True(brief >= 0, old.Stderr);
        Assert.Equal("[app] unknown in save 1: tone, race, nameLetter, nameSyllables, familyLetter, name", lines[brief + 1]);
        Assert.DoesNotContain("[app] name ", old.Stderr);
        Assert.DoesNotContain("tone", lines[brief]);                    // the brief line stays the save as written
        Assert.Equal("A character.", old.Stdout.Trim());

        Assert.True(full.ExitCode == 0, full.Stderr);
        Assert.Contains("[app] brief {", full.Stderr);
        Assert.DoesNotContain("unknown in save", full.Stderr);
        // A save with a name prints it, after the brief line (ADR-009).
        var fullLines = full.Stderr.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        Assert.Equal("[app] name Kessa Vale", fullLines[fullLines.FindIndex(l => l.StartsWith("[app] brief {")) + 1]);
    }

    private static async Task<ConsoleProcess.Result> LoadOf(string briefFields, string name = "")
    {
        var fixture = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "replay.jsonl");
        var file = Path.Combine(Path.GetTempPath(), $"npcforge-view-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(file, $$"""{ "1": { "brief": { {{briefFields}} },{{name}} "text": "A character." } }""");
            return await ConsoleProcess.RunAsync(
                ["--provider", "replay", "--recording", fixture, "--load", "1"],
                offline: true, environment: new() { ["NPCFORGE_CHARACTERS"] = file });
        }
        finally
        {
            File.Delete(file);
        }
    }
}
