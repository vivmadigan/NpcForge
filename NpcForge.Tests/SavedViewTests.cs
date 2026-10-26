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

    private static JsonElement Entry(string briefFields)
    {
        using var entry = JsonDocument.Parse($$"""{ "brief": { {{briefFields}} }, "text": "A character." }""");
        return entry.RootElement.Clone();
    }

    [Fact]
    public void An_old_save_lacks_the_five_fields_and_they_are_listed_in_schema_order()
    {
        var unknown = SavedView.Unknown(SceneDraftTests.Fields, Entry(OldBrief));

        Assert.Equal(["tone", "race", "nameLetter", "nameSyllables", "familyLetter"], unknown);
    }

    [Fact]
    public void A_save_with_every_field_has_nothing_unknown()
    {
        Assert.Empty(SavedView.Unknown(SceneDraftTests.Fields, Entry(OldBrief + NewFields)));
    }

    // Read from the schema it is handed, not from a list of its own: a field the console has never
    // heard of, added to the brief later, shows as unknown with no edit here.
    [Fact]
    public void A_field_added_to_the_schema_later_is_unknown_with_no_edit()
    {
        RollField[] fields = [.. SceneDraftTests.Fields, new("pet", "A pet", null, Required: false)];

        var unknown = SavedView.Unknown(fields, Entry(OldBrief + NewFields));

        Assert.Equal(["pet"], unknown);
    }

    // The process: --load on an old save prints the brief line exactly as saved, then the unknown line,
    // then the text. A save with every field prints no unknown line, which is the positive control: the
    // line is not simply always there. The replay provider and the dead proxy keep it free.
    [Fact]
    public async Task Load_prints_the_unknown_line_after_the_brief_line_for_an_old_save()
    {
        var old = await LoadOf(OldBrief);
        var full = await LoadOf(OldBrief + NewFields);

        Assert.True(old.ExitCode == 0, old.Stderr);
        var lines = old.Stderr.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var brief = lines.FindIndex(l => l.StartsWith("[app] brief {"));
        Assert.True(brief >= 0, old.Stderr);
        Assert.Equal("[app] unknown in save 1: tone, race, nameLetter, nameSyllables, familyLetter", lines[brief + 1]);
        Assert.DoesNotContain("tone", lines[brief]);                    // the brief line stays the save as written
        Assert.Equal("A character.", old.Stdout.Trim());

        Assert.True(full.ExitCode == 0, full.Stderr);
        Assert.Contains("[app] brief {", full.Stderr);
        Assert.DoesNotContain("unknown in save", full.Stderr);
    }

    private static async Task<ConsoleProcess.Result> LoadOf(string briefFields)
    {
        var fixture = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "replay.jsonl");
        var file = Path.Combine(Path.GetTempPath(), $"npcforge-view-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(file, $$"""{ "1": { "brief": { {{briefFields}} }, "text": "A character." } }""");
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
