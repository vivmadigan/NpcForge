using Microsoft.Extensions.AI;
using static NpcForge.Tests.McpToolSourceTests;    // RollArgs: the four answers every roll here starts from

namespace NpcForge.Tests;

// ADR-014, decision 9.3: the proof, before the console stops starting the server, that the core gives the
// model and the writer what the server gives them today. Each test reads the server's side from a real server
// process, as the console does, and never builds the server's tool in this process: here Abstractions is the
// console's 10.11.0, and the server runs 10.8.3, so an in-process copy would run both sides on one version and
// prove nothing about the switch. The server goes in the behavior PR and these go with it. Servers start one
// after another (lesson 012).
[Trait("Category", "Process")]
public class CoreMatchesServerTests
{
    [Fact]
    public async Task The_core_lists_the_roll_fields_the_server_lists_in_the_same_order()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);

        var server = RollField.ListFrom(tools.SchemaOf("roll_character")).Select(Describe);
        var core = Dice.Fields.Select(Describe);

        Assert.Equal(string.Join("\n", server), string.Join("\n", core));
    }

    [Fact]
    public async Task Lookup_archetype_reaches_the_model_as_the_server_sends_it()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);

        var served = (await tools.ListAsync(CancellationToken.None)).Single(tool => tool.Name == "lookup_archetype");

        // The replay compares tools by this text, so a schema that differs in property order fails here too.
        Assert.Equal(AsRecorded(served), AsRecorded(Archetypes.Lookup));

        // The text the model reads back. Once the console calls the core in-process, the writer's tool is a
        // ConsoleTool over Archetypes.Lookup (ADR-014, decision 5), so the core's side goes through it too.
        var call = new FunctionCallContent("call-1", "lookup_archetype", new Dictionary<string, object?> { ["occupation"] = "innkeeper" });
        var servedText = await tools.InvokeAsync(call, CancellationToken.None);
        var coreText = await new ConsoleTool(Archetypes.Lookup).InvokeAsync(call, CancellationToken.None);

        Assert.Equal(servedText, coreText);
    }

    // One server per seed, rolled several times in a row, with a Dice of the same seed rolled the same way,
    // so the dice carrying on from one roll into the next are held too. The arguments are the same
    // dictionary on both sides. An apostrophe, a letter beyond ASCII, markup characters and an emoji are in
    // them because escaping is where one JSON writer differs from another. The last roll is a lab's tone
    // variant (BriefJson.WithField, as Lab.cs rolls one) of the "playersWant not given" brief: every argument
    // but the tone is a JsonElement, null for the want, and AIFunctionFactory binds a JsonElement down a
    // different path than a string.
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    public async Task The_brief_text_the_writer_is_sent_is_the_text_the_server_returns_byte_for_byte(int seed)
    {
        await using var tools = new McpToolSource(seed: seed);
        await tools.ConnectAsync(CancellationToken.None);
        var dice = new Dice(seed);

        async Task<string> RollOnBothSides(string label, Dictionary<string, object?> arguments)
        {
            var server = await tools.CallDirectAsync("roll_character", arguments, CancellationToken.None);
            var core = BriefJson.Of(dice.Roll(arguments));

            Assert.True(server == core, $"seed {seed}, {label}: the core's brief text differs from the server's.\nserver: {server}\ncore:   {core}");
            return server;
        }

        var served = new Dictionary<string, string>();
        foreach (var (label, arguments) in RollsToCompare())
            served[label] = await RollOnBothSides(label, arguments);

        await RollOnBothSides("a lab's tone variant of the playersWant-not-given brief",
            BriefJson.WithField(served[PlayersWantNotGiven], "tone", "Heartfelt"));
    }

    private const string PlayersWantNotGiven = "playersWant not given, an apostrophe and a letter beyond ASCII in the setting";

    private static IEnumerable<(string Label, Dictionary<string, object?> Arguments)> RollsToCompare()
    {
        yield return ("four answers", RollArgs());

        var notGiven = RollArgs();
        notGiven["playersWant"] = null;
        notGiven["setting"] = "Gull's Rest at Ærø";
        yield return (PlayersWantNotGiven, notGiven);

        yield return ("every enum handed in", new Dictionary<string, object?>
        {
            ["setting"] = "a ferry",
            ["playersWant"] = "a crossing",
            ["occupation"] = "ferryman",
            ["difficulty"] = "Easy",
            ["obstacle"] = "ForAPrice",
            ["tone"] = "Comic",
            ["nameLetter"] = "Q",
            ["nameSyllables"] = "3",
            ["familyLetter"] = "Z",
            ["gender"] = "Female",
            ["age"] = "Old",
        });

        yield return ("every free-text trait handed in", new Dictionary<string, object?>
        {
            ["setting"] = "a market <stall> & a cart",
            ["playersWant"] = "“the name of a fence”",
            ["occupation"] = "smith's apprentice",
            ["characterWants"] = "to be left alone — mostly",
            ["attitude"] = "says \"no\" first",
            ["mannerism"] = "counts coins + sighs",
            ["weakness"] = "flattery from Ærø 🙂",
            ["willNotDiscuss"] = "her brother's debts",
            ["wrongAbout"] = "the harbour's tides",
            ["race"] = "half-orc",
            ["look"] = "a scar across one cheek",
        });

        yield return ("four answers again, after the handed-in rolls", RollArgs());
    }

    // One line for the whole field, so a difference in a name, a description, a choice or when it is
    // required shows as one line of the diff.
    private static string Describe(RollField field) =>
        $"{field.Name} | {field.WhenMissing} | {(field.Choices is null ? "free text" : "choices " + string.Join(", ", field.Choices))} | {field.Description}";

    // The text a replay compares tools by: name, description and the schema in the order it was written.
    private static string AsRecorded(AITool tool) =>
        Recording.Describe(RecordedRequest.From([], new ChatOptions { Tools = [tool] }));
}
