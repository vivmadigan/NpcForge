using System.Text.Json;

namespace NpcForge.Tests;

// Step 6's two promises, checked against the real server with no model: the model's list
// leaves out roll_character, and the app's own door rolls a brief. Free, like the others.
// It does not check that rolls differ: that is random, so a test of it would fail at random.
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
}
