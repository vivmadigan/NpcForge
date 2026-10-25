using Microsoft.Extensions.AI;
using System.Text.Json;
using Xunit.Abstractions;

namespace NpcForge.Tests;

// The scene interview end to end, free: a scripted stand-in model, the GM as a TextReader, and the
// real server for the roll that follows a yes. ADR-007 and the "Talk to it" epic's Done when.
public class SceneInterviewTests(ITestOutputHelper output)
{
    private static readonly IReadOnlyList<RollField> Fields = SceneDraftTests.Fields;
    private static readonly IReadOnlyDictionary<string, object?> Defaults = SceneDraftTests.Defaults;

    // Honest answers from the tavern scene, one invented free-text trait, one inferred difficulty.
    private static readonly FunctionCallContent[] TavernAnswers =
    [
        Answer("setting", "his tavern", "at his tavern"),
        Answer("occupation", "tavern owner", "My players meet a tavern owner"),
        Answer("playersWant", "ask about a thieves guild", "to ask about a thieves guild"),
        Answer("obstacle", "Cant", "He knows nothing about the guild"),
        Answer("attitude", "doesn't want to get involved", "and doesn’t want to get involved."),
        Answer("difficulty", "Wall", "doesn't want to get involved"),
        Answer("mannerism", "taps the bar nervously", "doesn't want to get involved"),
    ];

    // Done when: the model can't reach roll_character.
    [Fact]
    public async Task The_model_is_offered_only_record_answer_and_naming_roll_character_fails()
    {
        var model = new ScriptedInterviewer(
        [
            new FunctionCallContent("call-roll", "roll_character", new Dictionary<string, object?>
            {
                ["setting"] = "an inn", ["playersWant"] = "a name", ["difficulty"] = "Wall", ["occupation"] = "innkeeper",
            }),
            Answer("setting", "his tavern", "at his tavern"),
        ]);

        await Interview(model, SceneDraftTests.Tavern + "\nno\n");

        Assert.Equal(["record_answer"], model.Offered);
        Assert.Equal("Tool failed: No tool named 'roll_character'.", model.Results["call-roll"]);
        Assert.StartsWith("Recorded setting", model.Results["call-setting"]);
    }

    // Done when: nothing is rolled before my yes.
    [Theory]
    [InlineData("no\n")]
    [InlineData("y\n")]
    [InlineData("yes please\n")]
    [InlineData("")]                    // end of input after the confirmation
    public async Task Anything_but_yes_returns_nothing_to_roll(string reply)
    {
        var model = new ScriptedInterviewer(TavernAnswers);

        var (told, _) = await Interview(model, SceneDraftTests.Tavern + "\n" + reply);

        Assert.Null(told);
        Assert.Equal(2, model.Calls);   // the interview did run: the null is the GM's answer, not a skip
    }

    [Fact]
    public async Task End_of_input_before_a_scene_never_asks_the_model()
    {
        var model = new ScriptedInterviewer(TavernAnswers);

        var (told, screen) = await Interview(model, "\n   \n");

        Assert.Null(told);
        Assert.Equal(0, model.Calls);
        Assert.StartsWith(SceneInterview.Greeting, screen);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("  YES ")]
    public async Task Yes_returns_the_arguments_only_after_the_confirmation_was_shown(string reply)
    {
        var model = new ScriptedInterviewer(TavernAnswers);
        var screen = new StringWriter();
        var gm = new WatchedReader([SceneDraftTests.Tavern, reply], screen);

        var told = await SceneInterview.RunAsync(model, new ChatOptions(), Fields, Defaults, gm, screen, CancellationToken.None);

        Assert.NotNull(told);
        Assert.Equal("Cant", told.RollArguments["obstacle"]);
        // What the screen held when the GM was asked the second time: the whole confirmation.
        Assert.Contains("obstacle: Cant", gm.ScreenAtEachRead[1]);
        Assert.DoesNotContain("obstacle: Cant", gm.ScreenAtEachRead[0]);
        Assert.EndsWith(SceneInterview.Question + " ", gm.ScreenAtEachRead[1]);
    }

    // ADR-008: a yes hands the scene on for the writer, as the GM typed it, trimmed.
    [Fact]
    public async Task A_yes_hands_back_the_scene_as_typed()
    {
        var (told, _) = await Interview(new ScriptedInterviewer(TavernAnswers), "  " + SceneDraftTests.Tavern + "  \nyes\n");

        Assert.Equal(SceneDraftTests.Tavern, told!.Scene);
    }

    // A real model may send one call per turn and keep going. Out of turns, the GM still sees what
    // was recorded and decides; the paid run is not lost to a stack trace.
    [Fact]
    public async Task A_model_that_runs_out_of_turns_still_reaches_the_confirmation()
    {
        var model = new OneCallPerTurn(Answer("obstacle", "Cant", "He knows nothing about the guild"));
        var screen = new StringWriter { NewLine = "\n" };

        var told = await SceneInterview.RunAsync(model, new ChatOptions(), Fields, Defaults,
            new StringReader(SceneDraftTests.Tavern + "\nyes\n"), screen, CancellationToken.None);

        Assert.Equal(Fields.Count + 3, model.Calls);
        Assert.Contains("  obstacle: Cant   [inferred]", screen.ToString());
        Assert.Equal("Cant", told!.RollArguments["obstacle"]);
    }

    // The rehearsal of the tavern scene. Its screen is in the test output, and in the PR.
    [Fact]
    public async Task The_tavern_scene_shows_every_field_with_its_label_and_words()
    {
        var model = new ScriptedInterviewer(TavernAnswers);

        var (told, screen) = await Interview(model, SceneDraftTests.Tavern + "\nyes\n");
        output.WriteLine(screen);

        Assert.Contains("  setting: his tavern   [my words]\n    from \"at his tavern\"\n    in   \"My players meet a tavern owner at his tavern to ask about a thieves guild.\"", screen);
        Assert.Contains("  difficulty: Wall   [inferred]\n    from \"doesn't want to get involved\"", screen);
        Assert.Contains("  obstacle: Cant   [inferred]\n    from \"He knows nothing about the guild\"", screen);
        // The quote as the GM typed it, straight apostrophe and all, not as the model sent it.
        Assert.Contains("  attitude: doesn't want to get involved   [my words]\n    from \"and doesn't want to get involved\"", screen);
        Assert.Contains("  playersWant: ask about a thieves guild   [my words]", screen);
        // The invention was refused, so the dice roll it.
        Assert.Contains("  mannerism: [rolled]", screen);
        Assert.StartsWith("Rejected:", model.Results["call-mannerism"]);

        Assert.Equal(
            new Dictionary<string, object?>
            {
                ["setting"] = "his tavern",
                ["playersWant"] = "ask about a thieves guild",
                ["difficulty"] = "Wall",
                ["occupation"] = "tavern owner",
                ["obstacle"] = "Cant",
                ["attitude"] = "doesn't want to get involved",
            },
            told!.RollArguments);
    }

    // ADR-009: a stand-in interviewer that records a tone gives "tone: Comic [inferred]" on the screen,
    // and the five new fields the scene is silent about are rolled.
    [Fact]
    public async Task A_tone_read_from_the_scene_shows_as_inferred_and_the_silent_new_fields_are_rolled()
    {
        const string scene = "The players meet a clown at a fair. Everything about him is a joke.";
        var model = new ScriptedInterviewer([Answer("tone", "Comic", "Everything about him is a joke")]);

        var (told, screen) = await Interview(model, scene + "\nyes\n");

        Assert.Contains("  tone: Comic   [inferred]\n    from \"Everything about him is a joke\"", screen);
        foreach (var field in new[] { "race", "nameLetter", "nameSyllables", "familyLetter" })
            Assert.Contains($"  {field}: [rolled]", screen);
        Assert.Equal("Comic", told!.RollArguments["tone"]);
        Assert.False(told.RollArguments.ContainsKey("race"));
    }

    // ADR-009: a letter is recorded only when the GM said it. The stand-in claims K on a scene with no K,
    // then on one that says "starts with K".
    [Fact]
    public async Task A_name_letter_is_rejected_without_the_letter_in_the_quote_and_kept_with_it()
    {
        var withoutK = new ScriptedInterviewer([Answer("nameLetter", "K", "meet a tavern owner")]);
        var (toldWithout, screenWithout) = await Interview(withoutK, "My players meet a tavern owner.\nyes\n");

        Assert.StartsWith("Rejected:", withoutK.Results["call-nameLetter"]);
        Assert.Contains("  nameLetter: [rolled]", screenWithout);
        Assert.False(toldWithout!.RollArguments.ContainsKey("nameLetter"));

        var withK = new ScriptedInterviewer([Answer("nameLetter", "K", "starts with K")]);
        var (toldWith, screenWith) = await Interview(withK, "My players meet a baker whose name starts with K.\nyes\n");

        Assert.Contains("  nameLetter: K   [inferred]", screenWith);
        Assert.Equal("K", toldWith!.RollArguments["nameLetter"]);
    }

    // Done when: a trait I described reaches the brief instead of a roll. The real server, with a
    // seed whose plain roll gives a different obstacle, so the brief can only say Cant if the
    // interview's answer got there.
    [Fact]
    public async Task A_described_trait_reaches_the_brief_from_the_real_server()
    {
        const int seed = 1;
        var plain = await Roll(seed, RunOptions.Parse([]).RollArguments());
        Assert.NotEqual("Cant", plain.GetProperty("obstacle").GetString());

        var (told, _) = await Interview(new ScriptedInterviewer(TavernAnswers), SceneDraftTests.Tavern + "\nyes\n");
        var brief = await Roll(seed, told!.RollArguments);

        Assert.Equal("Cant", brief.GetProperty("obstacle").GetString());
        Assert.Equal("doesn't want to get involved", brief.GetProperty("attitude").GetString());
        Assert.Equal("tavern owner", brief.GetProperty("occupation").GetString());
        // Done when: a field I said nothing about is rolled. The dice picked one.
        Assert.False(string.IsNullOrWhiteSpace(brief.GetProperty("mannerism").GetString()));
    }

    // Program.cs, as a process: no answer flag means the interview, and end of input stops it with
    // nothing rolled and nothing saved. Free twice over: the replay provider, and no model turn.
    [Fact]
    public async Task The_app_with_no_answer_flag_waits_for_a_scene_and_end_of_input_rolls_nothing()
    {
        var fixture = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "replay.jsonl");
        var saveFile = Path.Combine(Path.GetTempPath(), $"npcforge-interview-{Guid.NewGuid():N}.json");
        try
        {
            var run = await ConsoleProcess.RunAsync(
                ["--provider", "replay", "--recording", fixture],
                offline: true,
                environment: new() { ["NPCFORGE_CHARACTERS"] = saveFile },
                input: "");

            Assert.True(run.ExitCode == 0, run.Stderr);
            Assert.Contains(SceneInterview.Greeting, run.Stderr);
            Assert.Contains("[app] not rolled: the GM did not say yes", run.Stderr);
            Assert.DoesNotContain("[app] brief", run.Stderr);
            Assert.DoesNotContain("[loop]", run.Stderr);
            Assert.Equal("", run.Stdout.Trim());
            Assert.False(File.Exists(saveFile));
        }
        finally
        {
            File.Delete(saveFile);
        }
    }

    private static FunctionCallContent Answer(string field, string value, string quote) =>
        new($"call-{field}", "record_answer", new Dictionary<string, object?>
        {
            ["field"] = field, ["value"] = value, ["quote"] = quote,
        });

    private static async Task<(Told? Told, string Screen)> Interview(IChatClient model, string typed)
    {
        var screen = new StringWriter { NewLine = "\n" };
        var told = await SceneInterview.RunAsync(
            model, new ChatOptions(), Fields, Defaults, new StringReader(typed), screen, CancellationToken.None);
        return (told, screen.ToString());
    }

    private static async Task<JsonElement> Roll(int seed, Dictionary<string, object?> arguments)
    {
        await using var tools = new McpToolSource(seed: seed);
        await tools.ConnectAsync(CancellationToken.None);
        using var brief = JsonDocument.Parse(await tools.CallDirectAsync("roll_character", arguments, CancellationToken.None));
        return brief.RootElement.Clone();
    }

    // A stand-in interviewer: its first turn is the calls it was given, its second a closing line.
    // It keeps what it was offered and what each call got back.
    private sealed class ScriptedInterviewer(IReadOnlyList<FunctionCallContent> calls) : IChatClient
    {
        public int Calls { get; private set; }
        public IReadOnlyList<string> Offered { get; private set; } = [];
        public Dictionary<string, string> Results { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Calls == 1)
            {
                Offered = [.. (options?.Tools ?? []).Select(t => t.Name)];
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [.. calls])));
            }

            foreach (var result in messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>())
                Results[result.CallId] = result.Result?.ToString() ?? "";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done.")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    // Records the same answer every turn and never stops on its own.
    private sealed class OneCallPerTurn(FunctionCallContent call) : IChatClient
    {
        public int Calls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            var next = new FunctionCallContent($"call-{Calls}", call.Name, call.Arguments);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [next])));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    // The GM's lines, one per read, noting what the screen held at each read.
    private sealed class WatchedReader(IReadOnlyList<string> lines, StringWriter screen) : TextReader
    {
        private int _next;
        public List<string> ScreenAtEachRead { get; } = [];

        public override string? ReadLine()
        {
            ScreenAtEachRead.Add(screen.ToString());
            return _next < lines.Count ? lines[_next++] : null;
        }

        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => new(ReadLine());
        public override Task<string?> ReadLineAsync() => Task.FromResult(ReadLine());
    }
}
