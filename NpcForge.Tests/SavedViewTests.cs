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

    // ADR-011: the three fields after the five. A save from between the two records has the five and
    // not these.
    private const string LookFields = """
        , "gender": "Female", "age": "Old", "look": "bald"
        """;

    // A name as the file store saves it.
    private const string Kessa = """ "name": { "syllables": ["Kes", "sa"], "family": "Vale" }, """;

    // An entry of the save file, read as the file store reads one, into the SavedCharacter the store
    // hands over.
    private static SavedCharacter Entry(string briefFields, string name = "") =>
        JsonSerializer.Deserialize<SavedCharacter>(
            $$"""{ "brief": { {{briefFields}} },{{name}} "text": "A character." }""", JsonSerializerOptions.Web)!;

    [Fact]
    public void An_old_save_lacks_the_eight_fields_and_a_name_and_they_are_listed_in_schema_order_then_name()
    {
        var unknown = SavedView.Unknown(SceneDraftTests.Fields, Entry(OldBrief));

        Assert.Equal(["tone", "race", "nameLetter", "nameSyllables", "familyLetter", "gender", "age", "look", "name"], unknown);
    }

    // ADR-011: a save from after the name dice and before the look dice shows exactly the three as
    // unknown, so gender, age and look are never invented for it.
    [Fact]
    public void A_save_from_before_the_look_dice_shows_gender_age_and_look_as_unknown()
    {
        var unknown = SavedView.Unknown(SceneDraftTests.Fields, Entry(OldBrief + NewFields, Kessa));

        Assert.Equal(["gender", "age", "look"], unknown);
    }

    [Fact]
    public void A_save_with_every_field_and_a_name_has_nothing_unknown()
    {
        Assert.Empty(SavedView.Unknown(SceneDraftTests.Fields, Entry(OldBrief + NewFields + LookFields, Kessa)));
    }

    [Fact]
    public void A_save_with_every_brief_field_and_no_name_lists_only_name()
    {
        Assert.Equal(["name"], SavedView.Unknown(SceneDraftTests.Fields, Entry(OldBrief + NewFields + LookFields)));
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

        var unknown = SavedView.Unknown(fields, Entry(OldBrief + NewFields + LookFields, Kessa));

        Assert.Equal(["pet"], unknown);
    }

    // The process: --load on an old save prints the brief line exactly as saved, then the unknown line,
    // then the text. A save with every field prints no unknown line, which is the positive control: the
    // line is not simply always there. The replay provider and the dead proxy keep it free.
    [Fact]
    [Trait("Category", "Process")]
    public async Task Load_prints_the_unknown_line_after_the_brief_line_for_an_old_save()
    {
        var old = await LoadOf(OldBrief);
        var full = await LoadOf(OldBrief + NewFields + LookFields, Kessa);
        var beforeLooks = await LoadOf(OldBrief + NewFields, Kessa);

        Assert.True(old.ExitCode == 0, old.Stderr);
        var lines = old.Stderr.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var brief = lines.FindIndex(l => l.StartsWith("[app] brief {"));
        Assert.True(brief >= 0, old.Stderr);
        Assert.Equal("[app] unknown in save 1: tone, race, nameLetter, nameSyllables, familyLetter, gender, age, look, name", lines[brief + 1]);
        Assert.DoesNotContain("[app] name ", old.Stderr);
        Assert.DoesNotContain("tone", lines[brief]);                    // the brief line stays the save as written
        Assert.Equal("A character.", old.Stdout.Trim());

        Assert.True(full.ExitCode == 0, full.Stderr);
        Assert.Contains("[app] brief {", full.Stderr);
        Assert.DoesNotContain("unknown in save", full.Stderr);
        // A save with a name prints it, after the brief line (ADR-009).
        var fullLines = full.Stderr.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        Assert.Equal("[app] name Kessa Vale", fullLines[fullLines.FindIndex(l => l.StartsWith("[app] brief {")) + 1]);

        // ADR-011: a save with the name dice and no look dice lists the three, and only the three.
        Assert.True(beforeLooks.ExitCode == 0, beforeLooks.Stderr);
        var unknownLine = beforeLooks.Stderr.Split('\n').Single(l => l.StartsWith("[app] unknown in save")).TrimEnd('\r');
        Assert.Equal("[app] unknown in save 1: gender, age, look", unknownLine);
    }

    // ADR-010: a saved null is a want the scene left out. A missing key is Unknown, a null is NotGiven,
    // and no field is ever in both.
    [Fact]
    public void A_null_in_the_save_is_not_given_and_a_missing_key_is_unknown_never_both()
    {
        var nullWant = OldBrief.Replace("\"playersWant\": \"the name of a fence\"", "\"playersWant\": null");

        var notGiven = SavedView.NotGiven(SceneDraftTests.Fields, Entry(nullWant + NewFields, Kessa));
        var unknown = SavedView.Unknown(SceneDraftTests.Fields, Entry(nullWant, Kessa));

        Assert.Equal(["playersWant"], notGiven);
        Assert.DoesNotContain("playersWant", unknown);
        Assert.Empty(SavedView.NotGiven(SceneDraftTests.Fields, Entry(OldBrief)));
    }

    [Fact]
    [Trait("Category", "Process")]
    public async Task Load_prints_the_not_given_line_for_a_no_want_save_and_the_unknown_line_for_an_old_one_never_both()
    {
        var noWant = await LoadOf(OldBrief.Replace("\"playersWant\": \"the name of a fence\"", "\"playersWant\": null") + NewFields + LookFields, Kessa);
        var old = await LoadOf(OldBrief);

        Assert.True(noWant.ExitCode == 0, noWant.Stderr);
        Assert.Contains("[app] not given in save 1: playersWant", noWant.Stderr);
        Assert.DoesNotContain("unknown in save", noWant.Stderr);

        Assert.True(old.ExitCode == 0, old.Stderr);
        Assert.Contains("unknown in save 1:", old.Stderr);
        Assert.DoesNotContain("not given in save", old.Stderr);
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
