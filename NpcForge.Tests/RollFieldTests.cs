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

        // roll_character's parameter order. Difficulty is after occupation because it is optional and
        // C# puts optional parameters last (ADR-010); CharacterBrief keeps its own order.
        Assert.Equal(
            ["setting", "playersWant", "occupation", "difficulty", "characterWants", "obstacle",
             "attitude", "mannerism", "weakness", "willNotDiscuss", "wrongAbout",
             "tone", "race", "nameLetter", "nameSyllables", "familyLetter"],
            fields.Select(f => f.Name));

        // Required means supplied, never rolled: these three take a flag default when the scene is
        // silent. Difficulty is rolled when missing (ADR-010).
        Assert.Equal(["setting", "playersWant", "occupation"],
            fields.Where(f => f.Required).Select(f => f.Name));

        // The choice fields, and the nullable ones without the SDK's null among their choices.
        Assert.Equal(["Easy", "SomeWork", "Wall"], fields.Single(f => f.Name == "difficulty").Choices);
        Assert.Equal(["Wont", "Cant", "ForAPrice"], fields.Single(f => f.Name == "obstacle").Choices);
        Assert.Equal(["Serious", "Comic", "Heartfelt"], fields.Single(f => f.Name == "tone").Choices);
        Assert.All(fields.Where(f => f.Name is not ("difficulty" or "obstacle" or "tone" or "nameLetter" or "nameSyllables" or "familyLetter")),
            f => Assert.Null(f.Choices));

        // The interviewer reads these to know each field.
        Assert.All(fields, f => Assert.False(string.IsNullOrWhiteSpace(f.Description)));
    }

    // ADR-010: the schema itself says which kind each required field is. playersWant is required and
    // admits null ("not given"); setting and occupation are required and do not. If a later SDK stops
    // writing the null, this fails, and the fallback is a server-owned list (ADR-010, decision 1).
    [Fact]
    public async Task The_schema_requires_three_fields_and_only_the_want_admits_null()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);
        var schema = tools.SchemaOf("roll_character");

        Assert.Equal(["setting", "playersWant", "occupation"],
            schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
        bool AdmitsNull(string field)
        {
            var type = schema.GetProperty("properties").GetProperty(field).GetProperty("type");
            return type.ValueKind == JsonValueKind.Array && type.EnumerateArray().Any(t => t.GetString() == "null");
        }
        Assert.True(AdmitsNull("playersWant"), schema.GetProperty("properties").GetProperty("playersWant").GetRawText());
        Assert.False(AdmitsNull("setting"));
        Assert.False(AdmitsNull("occupation"));
    }

    // SceneDraftTests.Fields is a hand copy of this list, so its tests run with no server. Held equal to
    // the real schema on names, order, choices and kinds (Required, for now), so the copy cannot drift
    // from the server. Descriptions are the copy's own short ones, so they are not compared.
    [Fact]
    public async Task The_scene_draft_tests_hand_copy_matches_the_real_schema()
    {
        var fields = await FieldsFromServer();

        Assert.Equal(fields.Select(f => (f.Name, f.Required, Choices: string.Join(",", f.Choices ?? []))),
            SceneDraftTests.Fields.Select(f => (f.Name, f.Required, Choices: string.Join(",", f.Choices ?? []))));
    }

    // ADR-009: the syllable count is a number in a string, "1", "2", "3". The attribute on the server's
    // enum must reach the schema, or the console would read the member names (One, Two, Three).
    [Fact]
    public async Task The_syllable_choices_are_written_as_digits()
    {
        var fields = await FieldsFromServer();

        Assert.Equal(["1", "2", "3"], fields.Single(f => f.Name == "nameSyllables").Choices);
    }

    // Both name letters offer the whole alphabet: the table the dice use leaves out Q, U, X, Y and Z,
    // but a GM may still ask for one.
    [Fact]
    public async Task Both_name_letters_offer_all_26_letters()
    {
        var fields = await FieldsFromServer();
        var alphabet = Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToList();

        Assert.Equal(alphabet, fields.Single(f => f.Name == "nameLetter").Choices);
        Assert.Equal(alphabet, fields.Single(f => f.Name == "familyLetter").Choices);
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
