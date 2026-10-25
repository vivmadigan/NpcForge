using Microsoft.Extensions.AI;
using System.ComponentModel;            // Description
using System.Security.Cryptography;     // SHA256
using System.Text;                      // Encoding
using System.Text.Json;                 // JsonSerializer

namespace NpcForge;

// What a yes hands back: the scene as the GM typed it, for the writer (ADR-008), and the arguments
// for roll_character. The scene never goes to the roll.
public sealed record Told(string Scene, Dictionary<string, object?> RollArguments);

// The scene interview (ADR-007): greet the GM, read the scene in one go, let a model record what the
// scene says through record_answer, show every field with its label and words, and hand back
// roll_character's arguments only after the GM types yes. It never rolls. It holds no tool source
// that reaches the server, so it cannot roll, save or load: that is Program.cs's job, after the yes.
public static class SceneInterview
{
    public const string Greeting =
        "Describe the scene in one go: where the players are, who they meet, what they want from that " +
        "person, and anything you already know about them. Press Enter when you are done.";

    public const string Question = "Roll this character? Type yes to roll it. Anything else stops without rolling.";

    // Returns the scene and roll_character's arguments after a "yes", and null after anything else,
    // end of input included, so a pipe with nothing left in it can never roll.
    public static async Task<Told?> RunAsync(
        IChatClient client,
        ChatOptions options,
        IReadOnlyList<RollField> fields,
        IReadOnlyDictionary<string, object?> defaults,
        TextReader gm,
        TextWriter screen,
        CancellationToken ct)
    {
        screen.WriteLine(Greeting);
        var scene = await ReadSceneAsync(gm, screen);
        if (scene is null)
            return null;

        var draft = new SceneDraft(scene, fields);

        // Guidance at index 0, the scene at index 1, word for word: the only words the model can
        // quote are the GM's. AgentLoop only appends after them.
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, Instructions(fields)),
            new(ChatRole.User, scene),
        };

        // Room for one call per field per turn, a few retries after a rejection, and a closing turn.
        // Out of turns is not a failure here: every answer in the draft already passed its checks,
        // and the GM still sees them all and decides.
        string closing;
        try
        {
            closing = await new AgentLoop(client, new RecordAnswerTool(draft), options, maxTurns: fields.Count + 3)
                .RunAsync(history, ct);
        }
        catch (TurnCapReachedException ex)
        {
            closing = $"stopped: {ex.Message} The confirmation shows what was recorded.";
        }

        // A trace only. Nothing the model says is read: what it recorded is in the draft (AGENTS.md,
        // never parse the model's prose).
        Console.Error.WriteLine($"[interview] {closing}");

        screen.WriteLine();
        WriteConfirmation(screen, draft.Lines(defaults));
        screen.WriteLine();
        screen.Write(Question + " ");

        var answer = await gm.ReadLineAsync(ct);
        if (answer is null)
            screen.WriteLine();         // end of input left the cursor after the question
        return answer?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true
            ? new Told(scene, draft.RollArguments(defaults))
            : null;
    }

    // One line, asked again while it is empty. Code asks again, not the model: there are no
    // follow-up questions. Null at end of input.
    private static async Task<string?> ReadSceneAsync(TextReader gm, TextWriter screen)
    {
        while (true)
        {
            screen.Write("> ");
            var line = await gm.ReadLineAsync();
            if (line is null)
            {
                screen.WriteLine();     // end of input left the cursor after the prompt
                return null;
            }
            if (!string.IsNullOrWhiteSpace(line))
                return line.Trim();
        }
    }

    // The interviewer skill, then the fields as the server lists them, so a field added on the server
    // reaches the model with no change here.
    private static string Instructions(IReadOnlyList<RollField> fields)
    {
        // Read from next to the exe, as the writer's skill is. It is product: what the model is told.
        var skillPath = Path.Combine(AppContext.BaseDirectory, "skills", "scene-interviewer", "SKILL.md");
        var skill = File.ReadAllText(skillPath);

        var list = string.Join("\n", fields.Select(field => field.Choices is null
            ? $"- `{field.Name}` (free text): {field.Description}"
            : $"- `{field.Name}` (choice: {string.Join(", ", field.Choices)}): {field.Description}"));

        var instructions = skill.TrimEnd() + "\n\n" + list + "\n";

        // The same fingerprint the writer's skill gets, over what the model is sent.
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(instructions)))[..8].ToLowerInvariant();
        Console.Error.WriteLine($"[skill] {skillPath} ({instructions.Length} chars, {fingerprint})");

        return instructions;
    }

    // Every field, its label, the words behind it and the whole sentence they came from (ADR-007).
    private static void WriteConfirmation(TextWriter screen, IReadOnlyList<ConfirmationLine> lines)
    {
        screen.WriteLine("Here is the character so far. Nothing is rolled until you say yes.");
        foreach (var line in lines)
        {
            screen.WriteLine();
            // The label names are ADR-007's, so the screen and the record use the same words.
            switch (line.Label)
            {
                case Label.MyWords:
                    screen.WriteLine($"  {line.Field.Name}: {line.Value}   [my words]");
                    screen.WriteLine($"    from \"{line.Quote}\"");
                    screen.WriteLine($"    in   \"{line.Sentence}\"");
                    break;
                case Label.Inferred:
                    screen.WriteLine($"  {line.Field.Name}: {line.Value}   [inferred]");
                    screen.WriteLine($"    from \"{line.Quote}\"");
                    screen.WriteLine($"    in   \"{line.Sentence}\"");
                    break;
                case Label.Default:
                    screen.WriteLine($"  {line.Field.Name}: {line.Value}   [default]");
                    screen.WriteLine("    nothing recorded, so it takes its usual default");
                    break;
                case Label.Rolled:
                    screen.WriteLine($"  {line.Field.Name}: [rolled]");
                    screen.WriteLine("    nothing recorded, so the dice pick it after your yes");
                    break;
            }
        }
    }

    // The interview's only tool. The model is offered record_answer and nothing else, so it cannot
    // reach roll_character, save_character or load_character (AGENTS.md, McpToolSource.AppOnly).
    private sealed class RecordAnswerTool : IToolSource
    {
        private readonly SceneDraft _draft;
        private readonly AIFunction _record;

        public RecordAnswerTool(SceneDraft draft)
        {
            _draft = draft;
            // No label argument: code decides the label from the field's kind (ADR-007).
            _record = AIFunctionFactory.Create((Func<string, string, string, string>)RecordAnswer, name: "record_answer");
        }

        [Description("Record what the GM said about one field of the character, with the GM's own words that say it.")]
        private string RecordAnswer(
            [Description("The field's name, from the list of fields")] string field,
            [Description("What the GM said the field is. For free text, words copied from the quote; for a choice, one of its choices")] string value,
            [Description("The GM's own words that say it, copied exactly from the scene: one sentence, at most 12 words")] string quote)
            => _draft.Record(field, value, quote).Message;

        public Task<IReadOnlyList<AITool>> ListAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AITool>>([_record]);

        // Same contract as the other sources: one [tool] trace line, and every failure comes back as
        // text the model can read.
        public async Task<string> InvokeAsync(FunctionCallContent call, CancellationToken ct)
        {
            try
            {
                Console.Error.WriteLine($"[tool] {call.Name} {JsonSerializer.Serialize(call.Arguments)}");

                // Only the name it was offered. roll_character and the rest fail here, by name.
                if (call.Name != _record.Name)
                    throw new InvalidOperationException($"No tool named '{call.Name}'.");

                var result = await _record.InvokeAsync(new AIFunctionArguments(call.Arguments), ct);
                var text = result is JsonElement element && element.ValueKind == JsonValueKind.String
                    ? element.GetString()!
                    : result?.ToString() ?? "";
                Console.Error.WriteLine($"[tool]   {text}");
                return text;
            }
            catch (Exception ex)
            {
                return $"Tool failed: {ex.Message}";          // a result, not an exception
            }
        }
    }
}
