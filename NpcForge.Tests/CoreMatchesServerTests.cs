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

        var servedText = await tools.InvokeAsync(
            new FunctionCallContent("call-1", "lookup_archetype", new Dictionary<string, object?> { ["occupation"] = "innkeeper" }),
            CancellationToken.None);
        var coreText = await Archetypes.Lookup.InvokeAsync(new AIFunctionArguments { ["occupation"] = "innkeeper" });

        Assert.Equal(servedText, coreText?.ToString());
    }

    // One server per seed, rolled several times in a row, with a Dice of the same seed rolled the same way,
    // so the dice carrying on from one roll into the next are held too. The arguments are the same
    // dictionary on both sides. An apostrophe, a letter beyond ASCII, markup characters and an emoji are in
    // them because escaping is where one JSON writer differs from another.
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(9)]
    public async Task The_brief_text_the_writer_is_sent_is_the_text_the_server_returns_byte_for_byte(int seed)
    {
        await using var tools = new McpToolSource(seed: seed);
        await tools.ConnectAsync(CancellationToken.None);
        var dice = new Dice(seed);

        foreach (var (label, arguments) in RollsToCompare())
        {
            var server = await tools.CallDirectAsync("roll_character", arguments, CancellationToken.None);
            var core = BriefJson.Of(dice.Roll(arguments));

            Assert.True(server == core, $"seed {seed}, {label}: the core's brief text differs from the server's.\nserver: {server}\ncore:   {core}");
        }
    }

    private static IEnumerable<(string Label, Dictionary<string, object?> Arguments)> RollsToCompare()
    {
        yield return ("four answers", RollArgs());

        var notGiven = RollArgs();
        notGiven["playersWant"] = null;
        notGiven["setting"] = "Gull's Rest at Ærø";
        yield return ("playersWant not given, an apostrophe and a letter beyond ASCII in the setting", notGiven);

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
