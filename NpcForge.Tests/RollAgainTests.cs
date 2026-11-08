using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Nodes;           // JsonNode
using System.Text.RegularExpressions;

namespace NpcForge.Tests;

// ADR-013, decisions 3 and 7: "again" rolls a new character for the same answers on the same server,
// and the spending cap stops a run cleanly. Each test runs the whole app as a process, free, against
// a recording or a copy made from one; no fixture is ever written. The save file is checked as exact,
// the printed lines as text the GM reads (lesson 006).
[Trait("Category", "Process")]
public class RollAgainTests : IDisposable
{
    // A paid run at --seed 1 with the replay fixture's answers that answered again, then save
    // (docs/runs/2026-11-08-01-epic4-checks.md). Two characters in eight model calls.
    private static readonly string RollAgainFixture =
        Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "roll-again.jsonl");

    private readonly List<string> _temps = [];

    public void Dispose()
    {
        foreach (var temp in _temps)
            File.Delete(temp);
    }

    private string TempFile(string kind, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"npcforge-{kind}-{Guid.NewGuid():N}{extension}");
        _temps.Add(path);
        return path;
    }

    // Line 1 of a recording is the flags it was made with; the test adds the replay provider.
    private static async Task<ConsoleProcess.Result> ReplayAsync(string recording, string input, string saveFile) =>
        await ConsoleProcess.RunAsync(
            [.. JsonSerializer.Deserialize<string[]>(File.ReadLines(recording).First())!, "--provider", "replay", "--recording", recording],
            input: input, environment: new() { ["NPCFORGE_CHARACTERS"] = saveFile });

    // Each `[app] brief` line's JSON, in the order the run printed them.
    private static List<JsonNode> Briefs(string stderr) =>
        [.. Regex.Matches(stderr, @"^\[app\] brief (\{.*\})\s*$", RegexOptions.Multiline)
            .Select(match => JsonNode.Parse(match.Groups[1].Value)!)];

    // Each `[app] name` line, in order: the name the namer gave, with the dice it followed.
    private static List<string> Names(string stderr) =>
        [.. Regex.Matches(stderr, @"^\[app\] name (.+?)\s*$", RegexOptions.Multiline).Select(match => match.Groups[1].Value)];

    private static int Count(string stderr, string pattern) => Regex.Matches(stderr, pattern, RegexOptions.Multiline).Count;

    // The text each character was written as: the writer's last reply, the one with no tool call.
    // Read from the recording with the library's own types, never from the console's.
    private static List<string> WrittenTexts(string recording) =>
        [.. RecordedRun.WriterCalls(File.ReadAllLines(recording))
            .Select(call => call["response"].Deserialize<ChatResponse>(AIJsonUtilities.DefaultOptions)!)
            .Where(reply => !reply.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Any())
            .Select(reply => reply.Text)];

    // Roadmap epic 4, "A roll again rolls new dice, keeps every answer I gave, calls the namer and the
    // writer again, and asks again about the new character. Only the character I save is saved."
    [Fact]
    public async Task Again_then_save_writes_a_second_character_from_new_dice_and_saves_only_that_one()
    {
        var saveFile = TempFile("again", ".json");
        var texts = WrittenTexts(RollAgainFixture);
        Assert.True(texts.Count == 2, $"The recording holds {texts.Count} written character(s); it needs 2.");

        var run = await ReplayAsync(RollAgainFixture, "again\nsave\n", saveFile);

        Assert.True(run.ExitCode == 0, run.Stderr);

        // ReplayChatClient stops a run that makes one call too many, but not one that makes too few.
        // AgentLoop prints one [loop] line per model call, so the run made every recorded call.
        var recordedCalls = File.ReadLines(RollAgainFixture).Count() - 1;
        Assert.True(Count(run.Stderr, @"^\[loop\] turn ") == recordedCalls,
            $"The recording has {recordedCalls} model calls and the run made {Count(run.Stderr, @"^\[loop\] turn ")}.\n{run.Stderr}");

        var briefs = Briefs(run.Stderr);
        Assert.True(briefs.Count == 2, run.Stderr);
        Assert.False(JsonNode.DeepEquals(briefs[0], briefs[1]), $"Both rolls gave {briefs[0]}: the dice started again.");
        // The answers stay across a roll again; only what was rolled changes.
        foreach (var answer in new[] { "setting", "playersWant", "difficulty", "occupation" })
            Assert.Equal(briefs[0][answer]!.ToJsonString(), briefs[1][answer]!.ToJsonString());

        // The namer and the writer ran for each character, and the question was asked after each.
        var names = Names(run.Stderr);
        Assert.True(names.Count == 2 && names[0] != names[1], string.Join("\n", names));
        Assert.Equal(2, Count(run.Stderr, @"^\[skill\] .*[\\/]namer[\\/]SKILL\.md "));
        Assert.Equal(2, Count(run.Stderr, @"^\[skill\] .*[\\/]npc-writer[\\/]SKILL\.md"));
        Assert.Equal(2, run.Stderr.Split(KeepQuestion.Prompt).Length - 1);
        // Both texts reached stdout. Checked by name, not word for word: ConsoleProcess reads the
        // child's stdout in the console's code page, so a curly quote comes back as "ÔÇ£". The save
        // below is checked exactly.
        foreach (var shown in names)
            Assert.Contains(shown[..shown.IndexOf(" (", StringComparison.Ordinal)], run.Stdout);

        // Only the character on the screen when the GM typed save: the second.
        var saves = JsonNode.Parse(File.ReadAllText(saveFile))!.AsObject();
        Assert.Equal(["1"], saves.Select(save => save.Key));
        var saved = saves["1"]!;
        Assert.True(JsonNode.DeepEquals(briefs[1], saved["brief"]), $"Saved brief: {saved["brief"]}");
        Assert.Equal(texts[1], saved["text"]!.GetValue<string>());
        var name = saved["name"]!;
        var full = $"{string.Concat(name["syllables"]!.AsArray().Select(s => s!.GetValue<string>()))} {name["family"]}";
        Assert.StartsWith($"{full} (", names[1]);
    }

    // Roadmap epic 4, "A run under a seed rolls again: the second character's dice differ from the
    // first, and the run gives the same two characters every time." Through the app, so a roll again
    // that started a new server, or re-seeded, would roll the first dice again and the replay would
    // stop at the second character's namer.
    [Fact]
    public async Task A_seeded_run_that_rolls_again_gives_the_same_two_characters_every_time()
    {
        var first = await ReplayAsync(RollAgainFixture, "again\nsave\n", TempFile("seed-a", ".json"));
        var second = await ReplayAsync(RollAgainFixture, "again\nsave\n", TempFile("seed-b", ".json"));

        Assert.True(first.ExitCode == 0, first.Stderr);
        Assert.True(second.ExitCode == 0, second.Stderr);

        var briefs = Briefs(first.Stderr);
        Assert.True(briefs.Count == 2, first.Stderr);
        Assert.False(JsonNode.DeepEquals(briefs[0], briefs[1]), $"Both rolls gave {briefs[0]}: the dice started again.");

        var again = Briefs(second.Stderr);
        Assert.True(again.Count == 2, second.Stderr);
        Assert.True(JsonNode.DeepEquals(briefs[0], again[0]) && JsonNode.DeepEquals(briefs[1], again[1]),
            $"First run:\n{briefs[0]}\n{briefs[1]}\nSecond run:\n{again[0]}\n{again[1]}");
        Assert.Equal(Names(first.Stderr), Names(second.Stderr));
        Assert.Equal(first.Stdout, second.Stdout);
    }

    // ADR-013 Consequences: with the recorded usage raised to the cap, the second brief prints and the
    // cap refuses the namer's first call for it. The run stops with a reason and exit code 1, and the
    // first character, shown and not saved, stays unsaved.
    [Fact]
    public async Task The_spending_cap_on_a_roll_again_stops_the_run_with_a_reason_and_saves_nothing()
    {
        var saveFile = TempFile("capped-save", ".json");
        var capped = CopyWithLastCallAtTheCap(RecordedRun.Fixture);

        var run = await ReplayAsync(capped, "again\n", saveFile);

        Assert.True(run.ExitCode == 1, $"Exit code {run.ExitCode}.\n{run.Stderr}");
        var stopped = Regex.Match(run.Stderr, @"^\[app\] stopped: Spending cap reached: .* Nothing was saved\.\s*$", RegexOptions.Multiline);
        Assert.True(stopped.Success, run.Stderr);
        // After the second brief: the cap refused the second character's first model call.
        Assert.Equal(2, Briefs(run.Stderr[..stopped.Index]).Count);
        Assert.DoesNotContain("Unhandled", run.Stderr);
        Assert.DoesNotContain("   at ", run.Stderr);
        Assert.False(File.Exists(saveFile));
    }

    // A temp copy of a recording whose last call reports the whole session's allowance, so the next
    // call, the roll again's namer, is refused before it is sent.
    private string CopyWithLastCallAtTheCap(string recording)
    {
        var lines = File.ReadAllLines(recording);
        var last = JsonNode.Parse(lines[^1])!;
        last["response"]!["usage"]!["totalTokenCount"] = SpendingCap.TokensPerSession;
        lines[^1] = last.ToJsonString(Recording.Json);

        var path = TempFile("capped", ".jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }
}
