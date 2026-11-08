using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Nodes;           // JsonNode
using System.Text.RegularExpressions;

namespace NpcForge.Tests;

// ADR-013, decisions 3 and 7: "again" rolls a new character for the same answers on the same server,
// and the spending cap stops a run cleanly. Each test runs the whole app as a process, free, against
// the replay fixture or a copy made from it; the fixture itself is never written. The save file is
// checked as exact, the printed lines as text the GM reads (lesson 006).
[Trait("Category", "Process")]
public class RollAgainTests : IDisposable
{
    private readonly string _saveFile = Path.Combine(Path.GetTempPath(), $"npcforge-again-{Guid.NewGuid():N}.json");
    private readonly List<string> _copies = [];

    public void Dispose()
    {
        File.Delete(_saveFile);
        foreach (var copy in _copies)
            File.Delete(copy);
    }

    private static string[] Flags => JsonSerializer.Deserialize<string[]>(File.ReadLines(RecordedRun.Fixture).First())!;

    private async Task<ConsoleProcess.Result> RunAsync(string recording, string input) =>
        await ConsoleProcess.RunAsync(
            [.. Flags, "--provider", "replay", "--recording", recording],
            input: input, environment: new() { ["NPCFORGE_CHARACTERS"] = _saveFile });

    // Each `[app] brief` line's JSON, in the order the run printed them.
    private static List<JsonNode> Briefs(string stderr) =>
        [.. Regex.Matches(stderr, @"^\[app\] brief (\{.*\})\s*$", RegexOptions.Multiline)
            .Select(match => JsonNode.Parse(match.Groups[1].Value)!)];

    // The fixture holds one character, so this replay can only go as far as the second roll: the
    // namer's first call for the new dice is a fifth model call, and the recording has four. It proves
    // "again" is an answer, that it rolls again with the same answers, and that the second roll takes
    // new dice from the same server under the same seed. It cannot prove the second character is named,
    // written or saved; the test below with two characters does.
    [Fact]
    public async Task Again_rolls_new_dice_for_the_same_answers_and_goes_on_to_name_them()
    {
        var run = await RunAsync(RecordedRun.Fixture, "again\n");

        var briefs = Briefs(run.Stderr);
        Assert.True(briefs.Count == 2, run.Stderr);
        Assert.False(JsonNode.DeepEquals(briefs[0], briefs[1]), $"Both rolls gave {briefs[0]}: the dice started again.");
        // The answers stay across a roll again; only what was rolled changes.
        foreach (var answer in new[] { "setting", "playersWant", "difficulty", "occupation" })
            Assert.Equal(briefs[0][answer]!.ToJsonString(), briefs[1][answer]!.ToJsonString());

        // The replay stopped the run at the second character's namer, after the second brief.
        Assert.Contains("The recording has 4 model calls and the app made another.", run.Stderr);
        Assert.False(File.Exists(_saveFile));
    }

    // ADR-013 Consequences: with the recorded usage raised to the cap, the second brief prints and the
    // cap refuses the namer's first call for it. The run stops with a reason and exit code 1, and the
    // first character, shown and not saved, stays unsaved.
    [Fact]
    public async Task The_spending_cap_on_a_roll_again_stops_the_run_with_a_reason_and_saves_nothing()
    {
        var capped = CopyWithLastCallAtTheCap();

        var run = await RunAsync(capped, "again\n");

        Assert.True(run.ExitCode == 1, $"Exit code {run.ExitCode}.\n{run.Stderr}");
        var stopped = Regex.Match(run.Stderr, @"^\[app\] stopped: Spending cap reached: .* Nothing was saved\.\s*$", RegexOptions.Multiline);
        Assert.True(stopped.Success, run.Stderr);
        // After the second brief: the cap refused the second character's first model call.
        Assert.Equal(2, Briefs(run.Stderr[..stopped.Index]).Count);
        Assert.DoesNotContain("Unhandled", run.Stderr);
        Assert.DoesNotContain("   at ", run.Stderr);
        Assert.False(File.Exists(_saveFile));
    }

    // The whole roll again path, end to end, until a paid recording with two characters exists. The
    // recording is built here, in process, from the same CharacterRun the app runs: the first character
    // answered by the fixture, the second by a stand-in model that names inside the dice and writes one
    // line. Both use one server under the fixture's seed, as the app must. The app is then replayed
    // against it and must send every request the build sent, so an app that rolled the second character
    // on a new server, or re-seeded, would send the first dice again and fail at the fifth call.
    [Fact]
    public async Task Again_then_save_writes_a_second_character_from_new_dice_and_saves_only_that_one()
    {
        var recording = await BuildTwoCharacterRecordingAsync();

        var run = await RunAsync(recording, "again\nsave\n");

        Assert.True(run.ExitCode == 0, run.Stderr);
        var briefs = Briefs(run.Stderr);
        Assert.True(briefs.Count == 2, run.Stderr);
        Assert.False(JsonNode.DeepEquals(briefs[0], briefs[1]));
        Assert.Equal(2, run.Stderr.Split(KeepQuestion.Prompt).Length - 1);
        Assert.Contains(SecondText, run.Stdout);

        // Only the character on the screen when the GM typed save: the second.
        var saves = JsonNode.Parse(File.ReadAllText(_saveFile))!.AsObject();
        Assert.Equal(["1"], saves.Select(save => save.Key));
        Assert.True(JsonNode.DeepEquals(briefs[1], saves["1"]!["brief"]), $"Saved brief: {saves["1"]!["brief"]}");
        Assert.Equal(SecondText, saves["1"]!["text"]!.GetValue<string>());
    }

    private const string SecondText = "The second character, written by the stand-in.";

    // A temp copy of the fixture whose last call reports the whole session's allowance, so the next
    // call, the roll again's namer, is refused before it is sent.
    private string CopyWithLastCallAtTheCap()
    {
        var lines = File.ReadAllLines(RecordedRun.Fixture);
        var last = JsonNode.Parse(lines[^1])!;
        last["response"]!["usage"]!["totalTokenCount"] = SpendingCap.TokensPerSession;
        lines[^1] = last.ToJsonString(Recording.Json);

        var path = Path.Combine(Path.GetTempPath(), $"npcforge-capped-{Guid.NewGuid():N}.jsonl");
        _copies.Add(path);
        File.WriteAllLines(path, lines);
        return path;
    }

    private async Task<string> BuildTwoCharacterRecordingAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"npcforge-two-{Guid.NewGuid():N}.jsonl");
        _copies.Add(path);
        var buildSave = Path.Combine(Path.GetTempPath(), $"npcforge-two-save-{Guid.NewGuid():N}.json");
        _copies.Add(buildSave);

        // As Program.cs builds them for this command line, so the app's replay can match every request.
        var run = RunOptions.Parse([.. Flags, "--provider", "replay", "--recording", path]);
        var options = new ChatOptions { ModelId = run.Model, MaxOutputTokens = 5000 };
        var ask = new Ask(run.RollArguments(), Scene: null, run.Provider);

        var model = new FixtureThenStandIn(
            new ReplayChatClient(RecordedRun.Fixture),
            RecordedCalls: File.ReadLines(RecordedRun.Fixture).Count() - 1,
            NamerStandIn.FollowsTheDice(SecondText));
        IChatClient client = new RecordingChatClient(model, path, run.RecordedFlags());

        await using var tools = new McpToolSource(buildSave, run.Seed);
        await tools.ConnectAsync(CancellationToken.None);
        await CharacterRun.RollAndWriteAsync(client, options, tools, ask, CancellationToken.None);
        await CharacterRun.RollAndWriteAsync(client, options, tools, ask, CancellationToken.None);
        return path;
    }

    // The fixture's calls first, checked as the replay checks them, then the stand-in for the rest.
    private sealed class FixtureThenStandIn(IChatClient fixture, int RecordedCalls, IChatClient standIn) : IChatClient
    {
        private int _calls;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            (_calls++ < RecordedCalls ? fixture : standIn).GetResponseAsync(messages, options, cancellationToken);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
