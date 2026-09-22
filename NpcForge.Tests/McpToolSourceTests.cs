using System.Text.Json;
using Microsoft.Extensions.AI;      // FunctionCallContent


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
                ["text"] = "saved by the app",
            }, CancellationToken.None);

            var save = new FunctionCallContent("call-1", "save_character", new Dictionary<string, object?>
            {
                ["brief"] = brief.RootElement,
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

    // A fresh file per test, so tests never share saves with each other or with you.
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
