using Microsoft.Extensions.AI;
using System.ComponentModel;            // Description
using System.Text.Json;                 // JsonDocument

namespace NpcForge;

// The namer (ADR-009 decision 4): a short loop before the writer whose one way out with a name is
// give_name, checked against the dice by code. No accepted name, no writer: there is no state in
// which the model skipped the check.
public static class Namer
{
    // Room for a name, two tries after a refusal, and the closing turn with no tool call.
    public const int MaxTurns = 4;

    // Read from next to the exe, as the writer's skill is. It is product: what the model is told.
    public static string SkillPath { get; } = Path.Combine(AppContext.BaseDirectory, "skills", "namer", "SKILL.md");

    // Returns the first name give_name accepted. Throws NoNameException when none was, so the caller
    // stops before the writer is paid for.
    public static async Task<NamedCharacter> RunAsync(
        IChatClient client, ChatOptions options, WriterInput input, CancellationToken ct)
    {
        var dice = NameDice.From(input.BriefJson);

        var system = SystemMessage();
        Console.Error.WriteLine(system.Trace);

        // Guidance at index 0, facts at index 1, as the writer's. AgentLoop only appends.
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, system.Text),
            new(ChatRole.User, FirstMessage(input.BriefJson, input.Scene)),
        };

        var giveName = new GiveName(dice);
        try
        {
            // A copy of the options, so give_name never reaches the writer's tool list.
            await new AgentLoop(client, giveName.Tool(), options.Clone(), MaxTurns).RunAsync(history, ct);
        }
        catch (TurnCapReachedException)
        {
            // Out of turns is a stop only with no name. A name accepted before the cap stands.
        }

        // Nothing the model says is read: the name is the arguments of the call code accepted
        // (AGENTS.md, never parse the model's prose).
        var name = giveName.Accepted ?? throw new NoNameException(dice);
        Console.Error.WriteLine(TraceLine(name, dice));
        return name;
    }

    // The accepted name beside the dice it follows, as a trace shows it.
    public static string TraceLine(NamedCharacter name, NameDice dice) => $"[app] name {name.Full} {dice}";

    // The path is a parameter only so a test can hand in a file with other line endings.
    public static (string Text, string Trace) SystemMessage(string? skillPath = null)
    {
        skillPath ??= SkillPath;
        // "\n" throughout, as the writer's is (ChatAgent.SystemMessage): git checks the file out with
        // CRLF on Windows, and one commit must send the same text and print the same fingerprint on
        // either machine, or a recording made on one would not replay on the other.
        var text = File.ReadAllText(skillPath).ReplaceLineEndings("\n");
        return (text, $"[skill] {skillPath} ({text.Length} chars, {Fingerprint.Of(text)})");
    }

    // What the namer is told: the race, the name dice and the scene, and nothing else of the brief.
    // Each die is written out in words by code, so the namer is told the dice and never reads JSON.
    public static string FirstMessage(string briefJson, string? scene)
    {
        var dice = NameDice.From(briefJson);
        using var brief = JsonDocument.Parse(briefJson);
        var race = brief.RootElement.GetProperty("race").GetString();

        var message =
            "Name this character.\n\n" +
            $"Race: {race}\n" +
            $"First name: starts with {dice.Letter}, {dice.Syllables} syllable{(dice.Syllables == 1 ? "" : "s")}\n" +
            $"Family name: starts with {dice.FamilyLetter}";

        // The scene goes in as typed and code never reads it, as the writer's does (ADR-008).
        return scene is null
            ? message
            : message + $"\n\nThis is the scene the GM described, in their own words.\n\n<scene>\n{scene}\n</scene>";
    }

    // give_name's state: the first name it accepted. The first accepted name wins and a later call is
    // refused, so the namer cannot swap it for one it likes better.
    private sealed class GiveName(NameDice dice)
    {
        public NamedCharacter? Accepted { get; private set; }

        public ConsoleTool Tool() => new(AIFunctionFactory.Create((Func<string[], string, string>)Give, name: "give_name"));

        [Description("Give the character's name: the first name split into its syllables, and the family name.")]
        private string Give(
            [Description("The first name split into its syllables, in order, such as [\"Kes\", \"sa\"] for Kessa")] string[] syllables,
            [Description("The family name, such as Vale")] string family)
        {
            if (Accepted is not null)
                return NameCheck.AlreadyNamed(Accepted).Reason;

            var (rejection, reason) = NameCheck.Check(dice, syllables, family);
            if (rejection == NameRejection.None)
                Accepted = new NamedCharacter([.. syllables.Select(s => s.Trim())], family.Trim());
            return reason;
        }
    }
}

// No name followed the dice in the namer's turns. Its own type, so a caller can stop the run before
// the writer and say why.
public sealed class NoNameException(NameDice dice)
    : InvalidOperationException($"no name followed the dice {dice}");
