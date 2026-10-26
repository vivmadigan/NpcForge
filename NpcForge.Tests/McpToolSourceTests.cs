using System.Text.Json;
using Microsoft.Extensions.AI;      // FunctionCallContent
using NpcForge.Server;              // Tables: the rows the dice roll from


namespace NpcForge.Tests;

// Step 6's two promises, step 7's carry-over and step 8's saving, checked against the real
// server with no model: the model's list leaves out the app-only tools, the app's own door
// rolls, saves and loads, and naming an app-only tool gets a refusal. Free, like the others.
// Every test that saves uses its own temp file, never your characters.json.

public class McpToolSourceTests
{
    [Fact]
    public async Task Rolls_a_brief_the_model_cannot_see()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);    // starts NpcForge.Server as a child process

        var modelSees = await tools.ListAsync(CancellationToken.None);
        var briefJson = await tools.CallDirectAsync("roll_character", new()
        {
            ["setting"] = "an inn in a major city",
            ["playersWant"] = "the name of a fence",
            ["difficulty"] = "Wall",
            ["occupation"] = "innkeeper",
        }, CancellationToken.None);

        // One list for the model: lookup_archetype, and nothing app-only.
        Assert.DoesNotContain(modelSees, t => t.Name == "roll_character");
        Assert.Contains(modelSees, t => t.Name == "lookup_archetype");

        // One door for the app: the brief comes back as JSON text. Parsed here only to check
        // it; the app itself never deserialises it, it hands the text to the model.
        using var brief = JsonDocument.Parse(briefJson);
        Assert.Equal("Wall", brief.RootElement.GetProperty("difficulty").GetString());
        Assert.Equal("innkeeper", brief.RootElement.GetProperty("occupation").GetString());
    }
    [Fact]
    public async Task Refuses_an_app_only_tool_the_model_names()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);

        // ListAsync never showed the model this name, but nothing stops it guessing one.
        // The arguments are the ones that would genuinely roll a brief: without the filter
        // this call succeeds, so the filter is the only thing this test can be failing on.
        var call = new FunctionCallContent("call-1", "roll_character", new Dictionary<string, object?>
        {
            ["setting"] = "an inn in a major city",
            ["playersWant"] = "the name of a fence",
            ["difficulty"] = "Wall",
            ["occupation"] = "innkeeper",
        });

        var result = await tools.InvokeAsync(call, CancellationToken.None);

        // A result, not an exception: the loop feeds this straight back to the model.
        Assert.StartsWith("Tool failed:", result);
        Assert.Contains("roll_character", result);
    }

    [Fact]
    public async Task Loads_a_saved_character_back_unchanged()
    {
        var file = TempSaveFile();
        try
        {
            await using var tools = new McpToolSource(file);
            await tools.ConnectAsync(CancellationToken.None);

            var briefJson = await tools.CallDirectAsync("roll_character", RollArgs(), CancellationToken.None);

            // Curly quotes, an em dash and line breaks: what a model actually writes, and what a
            // careless encoder would change on the way to the file and back.
            var text = "## Who they are\n**Mara Venn** keeps the inn’s books—and its secrets.\n“Why do you ask?”";

            // The brief goes as a JSON object, as Program.cs sends it. As text it would fail.
            using var brief = JsonDocument.Parse(briefJson);
            var savedAs = await tools.CallDirectAsync("save_character", new()
            {
                ["brief"] = brief.RootElement,
                ["name"] = KessaVale,
                ["text"] = text,
            }, CancellationToken.None);

            var savedJson = await tools.CallDirectAsync("load_character", new()
            {
                ["number"] = int.Parse(savedAs),
            }, CancellationToken.None);

            // Step 8's "Done when": the brief comes back byte for byte, so the [app] brief line
            // of a load matches the run's, and the text comes back exactly.
            using var saved = JsonDocument.Parse(savedJson);
            Assert.Equal("1", savedAs);
            Assert.Equal(briefJson, saved.RootElement.GetProperty("brief").GetRawText());
            Assert.Equal(text, saved.RootElement.GetProperty("text").GetString());
            Assert.Equal("""{"syllables":["Kes","sa"],"family":"Vale"}""", saved.RootElement.GetProperty("name").GetRawText());

            // The save went to the temp file. Without this, a path that never reached the server
            // would pass everything above while writing to your real characters.json.
            Assert.True(File.Exists(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Stops_on_a_number_that_was_never_saved()
    {
        var file = TempSaveFile();
        try
        {
            await using var tools = new McpToolSource(file);
            await tools.ConnectAsync(CancellationToken.None);

            // The app's door throws, so --load stops instead of handing the model an empty brief.
            // The reason is asserted, not just the throw: only the server's McpException says it.
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                tools.CallDirectAsync("load_character", new() { ["number"] = 99 }, CancellationToken.None));

            Assert.Contains("No character saved as 99", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Refuses_save_and_load_when_the_model_names_them()
    {
        var file = TempSaveFile();
        try
        {
            await using var tools = new McpToolSource(file);
            await tools.ConnectAsync(CancellationToken.None);

            // Lesson 004: arguments that would genuinely work. A real brief to save, and a number
            // the app has just saved. Without the AppOnly filter both calls succeed, so the filter
            // is the only thing these asserts can be failing on.
            var briefJson = await tools.CallDirectAsync("roll_character", RollArgs(), CancellationToken.None);
            using var brief = JsonDocument.Parse(briefJson);
            await tools.CallDirectAsync("save_character", new()
            {
                ["brief"] = brief.RootElement,
                ["name"] = KessaVale,
                ["text"] = "saved by the app",
            }, CancellationToken.None);

            var save = new FunctionCallContent("call-1", "save_character", new Dictionary<string, object?>
            {
                ["brief"] = brief.RootElement,
                ["name"] = KessaVale,
                ["text"] = "saved by the model",
            });
            var load = new FunctionCallContent("call-2", "load_character", new Dictionary<string, object?>
            {
                ["number"] = 1,
            });

            var modelSees = await tools.ListAsync(CancellationToken.None);
            var saveResult = await tools.InvokeAsync(save, CancellationToken.None);
            var loadResult = await tools.InvokeAsync(load, CancellationToken.None);

            Assert.DoesNotContain(modelSees, t => t.Name is "save_character" or "load_character");
            Assert.StartsWith("Tool failed:", saveResult);
            Assert.Contains("save_character", saveResult);
            Assert.StartsWith("Tool failed:", loadResult);
            Assert.Contains("load_character", loadResult);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task The_same_seed_rolls_the_same_brief()
    {
        // Two servers, one after the other, both given seed 1. If the seed never reached the
        // dice, two briefs would match about one time in six million.
        var first = await RollWithSeed(1);
        var second = await RollWithSeed(1);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task A_handed_in_trait_replaces_its_die_without_moving_the_others()
    {
        // ADR-009: every die is cast, so one seed rolls the same brief whatever is handed in.
        // Seed 5 is one where, on the old short-circuit, the dice after attitude visibly moved.
        var rolled = await RollWithSeed(5, RollArgs());
        var handed = RollArgs();
        handed["attitude"] = "handed in by the test";
        var withAttitude = await RollWithSeed(5, handed);

        var expected = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rolled)!;
        var actual = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(withAttitude)!;

        Assert.Equal("handed in by the test", actual["attitude"].GetString());
        foreach (var (name, value) in expected.Where(e => e.Key != "attitude"))
            Assert.True(value.ToString() == actual[name].ToString(), $"{name} moved when attitude was handed in");
    }

    // ADR-009: every brief carries the five new fields, with no flag and no default: the dice give them.
    [Fact]
    public async Task Every_brief_carries_the_five_new_fields_rolled_from_their_tables()
    {
        var brief = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await RollWithSeed(5))!;

        Assert.Contains(brief["tone"].GetString(), new[] { "Serious", "Comic", "Heartfelt" });
        Assert.Contains(brief["race"].GetString(), Tables.Races.Select(r => r.Value));
        Assert.Contains(brief["nameLetter"].GetString(), Tables.NameLetters.Select(l => l.Value.ToString()));
        Assert.Contains(brief["nameSyllables"].GetString(), new[] { "1", "2", "3" });     // a string of a digit, on the wire
        Assert.Contains(brief["familyLetter"].GetString(), Tables.NameLetters.Select(l => l.Value.ToString()));
    }

    // A given tone, race, letter, count or family letter is kept; the one not given is rolled.
    [Fact]
    public async Task A_given_value_is_kept_and_a_value_not_given_is_rolled()
    {
        var args = RollArgs();
        args["tone"] = "Heartfelt";
        args["race"] = "half-orc";
        args["nameLetter"] = "Q";                // outside the dice's table, still allowed when handed in
        args["nameSyllables"] = "3";

        var brief = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await RollWithSeed(5, args))!;

        Assert.Equal("Heartfelt", brief["tone"].GetString());
        Assert.Equal("half-orc", brief["race"].GetString());
        Assert.Equal("Q", brief["nameLetter"].GetString());
        Assert.Equal("3", brief["nameSyllables"].GetString());
        Assert.Contains(brief["familyLetter"].GetString(), Tables.NameLetters.Select(l => l.Value.ToString()));   // rolled
    }

    // Done when: under one seed, --tone Comic and --tone Serious roll the same brief except for tone.
    [Fact]
    public async Task One_seed_rolls_the_same_brief_whatever_tone_is_given()
    {
        var comic = RollArgs();
        comic["tone"] = "Comic";
        var serious = RollArgs();
        serious["tone"] = "Serious";

        var withComic = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await RollWithSeed(5, comic))!;
        var withSerious = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await RollWithSeed(5, serious))!;

        // The third roll gives no tone. Two given tones could skip the tone die alike and still match
        // each other, so the control is the roll where the die is the only thing that picks.
        var untoned = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await RollWithSeed(5))!;

        Assert.Equal("Comic", withComic["tone"].GetString());
        Assert.Equal("Serious", withSerious["tone"].GetString());
        foreach (var (name, value) in untoned.Where(e => e.Key != "tone"))
        {
            Assert.True(value.ToString() == withComic[name].ToString(), $"{name} moved with the tone");
            Assert.True(value.ToString() == withSerious[name].ToString(), $"{name} moved with the tone");
        }
    }

    // Each new die, handed in, replaces its result and moves no other die.
    [Theory]
    [InlineData("tone", "Heartfelt")]
    [InlineData("race", "handed-in race")]
    [InlineData("nameLetter", "Q")]
    [InlineData("nameSyllables", "3")]
    [InlineData("familyLetter", "Q")]
    public async Task A_handed_in_new_value_replaces_its_die_without_moving_the_others(string parameter, string value)
    {
        var rolled = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await RollWithSeed(5))!;
        var handed = RollArgs();
        handed[parameter] = value;
        var withValue = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await RollWithSeed(5, handed))!;

        Assert.Equal(value, withValue[parameter].GetString());
        foreach (var (name, was) in rolled.Where(e => e.Key != parameter))
            Assert.True(was.ToString() == withValue[name].ToString(), $"{name} moved when {parameter} was handed in");
    }

    // The new dice come after the old seven, so a seed's old traits stay as they were. The fixture's
    // brief was rolled by the code before the five fields; its seven traits must still be what the
    // same seed rolls now. Read from the fixture with the library's types, like ReplayTests does.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_seeds_old_seven_traits_are_what_they_were_before_the_new_dice(bool namerCallFirst)
    {
        // Run on the fixture as recorded and on a copy with a namer call in front, as a re-recorded
        // fixture will have: the writer's call is found by what it is, not by its line.
        var fixture = namerCallFirst ? RecordedRun.CopyWithNamerFirst() : RecordedRun.Fixture;
        string[] lines;
        try { lines = File.ReadAllLines(fixture); }
        finally { if (namerCallFirst) File.Delete(fixture); }
        var run = RunOptions.Parse(JsonSerializer.Deserialize<string[]>(lines[0])!);
        var recorded = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            RecordedRun.BriefJson(RecordedRun.WriterCalls(lines)[0]))!;

        var now = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(await RollWithSeed(run.Seed!.Value, run.RollArguments()))!;

        foreach (var trait in new[] { "characterWants", "obstacle", "attitude", "mannerism", "weakness", "willNotDiscuss", "wrongAbout" })
            Assert.Equal(recorded[trait].GetString(), now[trait].GetString());
    }

    // Done when: --tone 7 is refused by name. System.Text.Json's string-enum converter takes a number
    // too, so without the check it would bind and be written back as a number. Every enum parameter,
    // difficulty and obstacle included, and the number as text as well as a number (the console sends text).
    [Theory]
    [InlineData("tone", 7)]
    [InlineData("tone", "7")]
    [InlineData("difficulty", 9)]
    [InlineData("difficulty", "9")]
    [InlineData("obstacle", 9)]
    [InlineData("obstacle", "9")]
    [InlineData("nameLetter", 99)]
    [InlineData("nameLetter", "99")]
    [InlineData("nameSyllables", 4)]
    [InlineData("nameSyllables", "0")]
    [InlineData("familyLetter", 99)]
    [InlineData("familyLetter", "99")]
    public async Task An_enum_value_that_is_not_defined_is_refused_by_the_name_of_its_field(string parameter, object value)
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);
        var args = RollArgs();
        args[parameter] = value;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.CallDirectAsync("roll_character", args, CancellationToken.None));

        // The reason, not just a failure (lesson 004): the server's own words, naming the field.
        Assert.Contains($"{parameter} {value} is not one of its values", error.Message);
    }

    private static async Task<string> RollWithSeed(int seed, Dictionary<string, object?> args)
    {
        await using var tools = new McpToolSource(seed: seed);
        await tools.ConnectAsync(CancellationToken.None);
        return await tools.CallDirectAsync("roll_character", args, CancellationToken.None);
    }

    // A server of its own each time, so the second roll cannot be the first one's dice carrying on.
    private static async Task<string> RollWithSeed(int seed)
    {
        await using var tools = new McpToolSource(seed: seed);
        await tools.ConnectAsync(CancellationToken.None);
        return await tools.CallDirectAsync("roll_character", RollArgs(), CancellationToken.None);
    }

    // A fresh file per test, so tests never share saves with each other or with you.
    // A name as Program.cs sends it: a JSON object, the first name split as the namer split it.
    private static readonly JsonElement KessaVale =
        JsonSerializer.SerializeToElement(new { syllables = new[] { "Kes", "sa" }, family = "Vale" });

    // ADR-009: the server binds the brief through CharacterBrief, so a brief that lacks a name die is
    // refused and nothing is saved. Everything else in the call would save (lesson 004): the control
    // saves the same brief with nameLetter.
    [Fact]
    public async Task Save_refuses_a_brief_without_nameLetter_and_saves_nothing()
    {
        var file = TempSaveFile();
        try
        {
            await using var tools = new McpToolSource(file);
            await tools.ConnectAsync(CancellationToken.None);
            var briefJson = await tools.CallDirectAsync("roll_character", RollArgs(), CancellationToken.None);
            var full = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(briefJson)!;
            var lacking = new Dictionary<string, JsonElement>(full);
            Assert.True(lacking.Remove("nameLetter"));

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => tools.CallDirectAsync("save_character", new()
            {
                ["brief"] = JsonSerializer.SerializeToElement(lacking),
                ["name"] = KessaVale,
                ["text"] = "A character.",
            }, CancellationToken.None));
            Assert.False(File.Exists(file), "a refused save writes nothing");

            var savedAs = await tools.CallDirectAsync("save_character", new()
            {
                ["brief"] = JsonSerializer.SerializeToElement(full),
                ["name"] = KessaVale,
                ["text"] = "A character.",
            }, CancellationToken.None);

            // The SDK reports a binding failure without its reason, so the control above, the same
            // call with nameLetter, is what shows the missing field is why it was refused.
            Assert.Contains("save_character failed", refused.Message);
            Assert.Equal("1", savedAs);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string TempSaveFile() =>
        Path.Combine(Path.GetTempPath(), $"npcforge-test-{Guid.NewGuid():N}.json");

    private static Dictionary<string, object?> RollArgs() => new()
    {
        ["setting"] = "an inn in a major city",
        ["playersWant"] = "the name of a fence",
        ["difficulty"] = "Wall",
        ["occupation"] = "innkeeper",
    };
}
