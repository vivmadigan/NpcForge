using System.Text.Json;
using Microsoft.Extensions.AI;      // FunctionCallContent


namespace NpcForge.Tests;

// Step 6's two promises, step 7's carry-over and step 8's saving, checked against the real
// server with no model: the model's list leaves out the app-only tools, the app's own door
// rolls, saves and loads, and naming an app-only tool gets a refusal. Free, like the others.
// Every test that saves uses its own temp file, never your characters.json.

[Trait("Category", "Process")]
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
    [InlineData("gender", 9)]
    [InlineData("gender", "9")]
    [InlineData("age", 9)]
    [InlineData("age", "9")]
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

    // ADR-011: a blank look would reach the writer as a look of nothing, so it is refused by name, as a
    // blank want is. The control is the same call with the look left out, which rolls one.
    [Fact]
    public async Task A_blank_look_is_refused_by_name_and_a_left_out_look_rolls()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);

        foreach (var blank in new[] { "", "   " })
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                tools.CallDirectAsync("roll_character", RollArgs("look", blank), CancellationToken.None));
            Assert.Contains("look is blank", error.Message);
        }
        var rolled = await tools.CallDirectAsync("roll_character", RollArgs(), CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(JsonDocument.Parse(rolled).RootElement.GetProperty("look").GetString()));
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

    // ADR-010: a want not given is a null, and the null is written. A missing key would read as a save
    // from before the record, shown as unknown.
    [Fact]
    public async Task A_null_want_comes_back_as_a_written_null()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);
        var args = RollArgs();
        args["playersWant"] = null;

        var briefJson = await tools.CallDirectAsync("roll_character", args, CancellationToken.None);

        using var brief = JsonDocument.Parse(briefJson);
        Assert.True(brief.RootElement.TryGetProperty("playersWant", out var want), $"no playersWant key in {briefJson}");
        Assert.Equal(JsonValueKind.Null, want.ValueKind);
    }

    // The schema says "you must say": a forgotten want is refused, a null one rolls. The control is the
    // same call with the null, so the missing key is the only reason for the refusal (lesson 004).
    [Fact]
    public async Task An_omitted_want_is_refused_while_a_null_one_rolls()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);
        var omitted = RollArgs();
        Assert.True(omitted.Remove("playersWant"));
        var nulled = RollArgs();
        nulled["playersWant"] = null;

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.CallDirectAsync("roll_character", omitted, CancellationToken.None));
        var rolled = await tools.CallDirectAsync("roll_character", nulled, CancellationToken.None);

        Assert.Contains("roll_character failed", refused.Message);
        Assert.Equal("innkeeper", JsonDocument.Parse(rolled).RootElement.GetProperty("occupation").GetString());
    }

    // "Not given" has one spelling. A blank want is refused in the server's own words, by name.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_want_is_refused_by_name(string blank)
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);
        var args = RollArgs();
        args["playersWant"] = blank;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.CallDirectAsync("roll_character", args, CancellationToken.None));

        Assert.Contains("playersWant is blank", error.Message);
    }

    // save_character binds through CharacterBrief: the null is kept in the file, and the same brief with
    // the key taken out is refused and saves nothing.
    [Fact]
    public async Task Save_keeps_a_null_want_and_refuses_the_brief_without_the_key()
    {
        var file = TempSaveFile();
        try
        {
            await using var tools = new McpToolSource(file);
            await tools.ConnectAsync(CancellationToken.None);
            var args = RollArgs();
            args["playersWant"] = null;
            var full = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                await tools.CallDirectAsync("roll_character", args, CancellationToken.None))!;
            var lacking = new Dictionary<string, JsonElement>(full);
            Assert.True(lacking.Remove("playersWant"));

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

            Assert.Contains("save_character failed", refused.Message);
            Assert.Equal("1", savedAs);
            // The file itself, not a load: the null must be on disk as a key.
            using var saved = JsonDocument.Parse(File.ReadAllText(file));
            var brief = saved.RootElement.GetProperty("1").GetProperty("brief");
            Assert.True(brief.TryGetProperty("playersWant", out var want), "the saved brief has no playersWant key");
            Assert.Equal(JsonValueKind.Null, want.ValueKind);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // ADR-013, decision 4. The server answers with each save's number, name, occupation and setting in
    // number order, and nothing else of the save. The file is out of order and holds an old save with no
    // name and a brief with no occupation, so a sort by text, a required name or a required key fails.
    [Fact]
    public async Task Lists_every_save_in_number_order_without_reading_the_text()
    {
        var file = SavedFile.WriteTemp();
        try
        {
            await using var tools = new McpToolSource(file);
            await tools.ConnectAsync(CancellationToken.None);

            var listed = await tools.CallDirectAsync("list_characters", new(), CancellationToken.None);

            Assert.DoesNotContain(SavedFile.TextMarker, listed);
            var rows = JsonDocument.Parse(listed).RootElement.EnumerateArray().ToList();
            Assert.Equal([8, 9, 10], rows.Select(row => row.GetProperty("number").GetInt32()));

            var bakery = rows[0];
            Assert.Equal("goblin baker", bakery.GetProperty("occupation").GetString());
            Assert.Equal("tiny bakery", bakery.GetProperty("setting").GetString());
            Assert.True(!bakery.TryGetProperty("name", out var none) || none.ValueKind == JsonValueKind.Null);

            // The name keeps the shape the save holds, so the console reads it like a loaded save's.
            var inn = rows[1].GetProperty("name");
            Assert.Equal(["Har", "grim"], inn.GetProperty("syllables").EnumerateArray().Select(s => s.GetString()));
            Assert.Equal("Rockhammer", inn.GetProperty("family").GetString());

            var barge = rows[2];
            Assert.True(!barge.TryGetProperty("occupation", out var noJob) || noJob.ValueKind == JsonValueKind.Null);
            Assert.Equal("a river barge", barge.GetProperty("setting").GetString());
        }
        finally
        {
            File.Delete(file);
        }
    }

    // ADR-013, decision 4: list_characters is app-only, so the model's list lacks it and a model that
    // names it anyway is refused. The save file holds real saves, so without the AppOnly entry the
    // calls succeed and the asserts fail on the filter alone (lesson 004).
    [Fact]
    public async Task Keeps_list_characters_from_the_model()
    {
        var file = SavedFile.WriteTemp();
        try
        {
            await using var tools = new McpToolSource(file);
            await tools.ConnectAsync(CancellationToken.None);

            var modelSees = await tools.ListAsync(CancellationToken.None);
            var result = await tools.InvokeAsync(
                new FunctionCallContent("call-1", "list_characters", new Dictionary<string, object?>()), CancellationToken.None);

            Assert.DoesNotContain(modelSees, t => t.Name == "list_characters");
            Assert.StartsWith("Tool failed:", result);
            Assert.Contains("list_characters", result);
            Assert.DoesNotContain("goblin baker", result);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string TempSaveFile() =>
        Path.Combine(Path.GetTempPath(), $"npcforge-test-{Guid.NewGuid():N}.json");

    internal static Dictionary<string, object?> RollArgs() => new()
    {
        ["setting"] = "an inn in a major city",
        ["playersWant"] = "the name of a fence",
        ["difficulty"] = "Wall",
        ["occupation"] = "innkeeper",
    };

    internal static Dictionary<string, object?> RollArgs(string handedIn, string value)
    {
        var args = RollArgs();
        args[handedIn] = value;
        return args;
    }
}
