using System.Text.Json;

namespace NpcForge.Tests;

// The interview's field list, read from the real server's tools/list (ADR-007). Free: the server is
// a child process and no model is asked. This is where a server change to roll_character shows up
// first, before any paid run.
public class RollFieldTests
{
    [Fact]
    public async Task Reads_every_roll_character_field_from_the_real_server()
    {
        var fields = await FieldsFromServer();

        // The brief's order, as CharacterBrief and roll_character have it.
        Assert.Equal(
            ["setting", "playersWant", "difficulty", "occupation", "characterWants", "obstacle",
             "attitude", "mannerism", "weakness", "willNotDiscuss", "wrongAbout"],
            fields.Select(f => f.Name));

        // Required means supplied, never rolled: these four take a flag default when the scene is silent.
        Assert.Equal(["setting", "playersWant", "difficulty", "occupation"],
            fields.Where(f => f.Required).Select(f => f.Name));

        // The two choice fields, and the nullable one without the SDK's null among its choices.
        Assert.Equal(["Easy", "SomeWork", "Wall"], fields.Single(f => f.Name == "difficulty").Choices);
        Assert.Equal(["Wont", "Cant", "ForAPrice"], fields.Single(f => f.Name == "obstacle").Choices);
        Assert.All(fields.Where(f => f.Name is not ("difficulty" or "obstacle")), f => Assert.Null(f.Choices));

        // The interviewer reads these to know each field.
        Assert.All(fields, f => Assert.False(string.IsNullOrWhiteSpace(f.Description)));
    }

    [Fact]
    public async Task Every_required_field_has_a_flag_default()
    {
        var fields = await FieldsFromServer();
        var defaults = RunOptions.Parse([]).RollArguments();

        // A required parameter the flag table does not know would leave the interview with nothing
        // to roll with when the scene is silent. Caught here, not after a GM's yes.
        Assert.All(fields.Where(f => f.Required), f => Assert.True(defaults.ContainsKey(f.Name), $"No default for {f.Name}"));
    }

    [Fact]
    public void A_field_with_no_description_stops_the_list()
    {
        using var schema = JsonDocument.Parse("""
            {"type":"object","properties":{"setting":{"type":"string"}},"required":["setting"]}
            """);

        var error = Assert.Throws<InvalidOperationException>(() => RollField.ListFrom(schema.RootElement));

        Assert.Contains("'setting' has no description", error.Message);
    }

    [Fact]
    public void A_field_that_is_not_text_stops_the_list()
    {
        using var schema = JsonDocument.Parse("""
            {"type":"object","properties":{"age":{"type":"integer","description":"How old"}}}
            """);

        var error = Assert.Throws<InvalidOperationException>(() => RollField.ListFrom(schema.RootElement));

        Assert.Contains("'age' is not text", error.Message);
    }

    private static async Task<IReadOnlyList<RollField>> FieldsFromServer()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);
        return RollField.ListFrom(tools.SchemaOf("roll_character"));
    }
}
