using System.Text.Json;
using Microsoft.Extensions.AI;      // FunctionCallContent


namespace NpcForge.Tests;

// Step 6's two promises and step 7's carry-over, checked against the real server with no
// model: the model's list leaves out roll_character, the app's own door rolls a brief, and
// naming an app-only tool gets a refusal. Free, like the others.

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

}
