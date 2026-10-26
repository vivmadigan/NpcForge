using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// The whole app as a process, naming a character (ADR-009 decision 4). No fixture holds a namer, so
// each test first makes its own recording: the real server rolls the brief, and a stand-in model
// plays the namer and the writer through RecordingChatClient. The app then replays that recording,
// free and offline, and the test reads what a user sees: the trace, the answer and the save.
public class NamedRunTests
{
    // The dice are given, so the stand-in's name is known: K, 2 syllables, V gives Kara Vale.
    private static readonly string[] Flags =
        ["--seed", "1", "--setting", "a mill", "--occupation", "miller", "--race", "dwarf",
         "--letter", "K", "--syllables", "2", "--family-letter", "V"];

    [Fact]
    public async Task A_named_run_prints_the_name_and_its_dice_before_the_writer_and_writes()
    {
        await using var made = await MadeRun.Of(NamerStandIn.FollowsTheDice(), Flags);

        var run = await made.ReplayAsync();

        Assert.True(run.ExitCode == 0, run.Stderr);
        var name = run.Stderr.IndexOf("[app] name Kara Vale (K, 2, V)", StringComparison.Ordinal);
        Assert.True(name >= 0, run.Stderr);
        // Named before the writer is told anything: the name line comes before the writer's skill line.
        Assert.True(name < run.Stderr.IndexOf("npc-writer", StringComparison.Ordinal), run.Stderr);
        Assert.Equal("A character.", run.Stdout.Trim());
        Assert.True(File.Exists(made.SaveFile), "a named run saves");
    }

    // The save keeps the name as the namer split it, and --load prints it after the brief line.
    [Fact]
    public async Task The_save_keeps_the_name_and_load_prints_it()
    {
        await using var made = await MadeRun.Of(NamerStandIn.FollowsTheDice(), Flags);
        var run = await made.ReplayAsync();
        Assert.True(run.ExitCode == 0, run.Stderr);

        var saved = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(made.SaveFile))!["1"]!;
        Assert.Equal("""{"syllables":["Ka","ra"],"family":"Vale"}""", saved["name"]!.ToJsonString());

        var load = await made.LoadAsync(1);
        Assert.True(load.ExitCode == 0, load.Stderr);
        var lines = load.Stderr.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        Assert.Equal("[app] name Kara Vale", lines[lines.FindIndex(l => l.StartsWith("[app] brief {")) + 1]);
        Assert.DoesNotContain("unknown in save", load.Stderr);
        Assert.Equal("A character.", load.Stdout.Trim());
    }

    // No name, no writer, no save. The replay holds only the namer's four calls, so a writer call
    // would also fail the replay with "made another".
    [Fact]
    public async Task A_namer_that_never_follows_the_dice_stops_the_run_before_the_writer_and_saves_nothing()
    {
        await using var made = await MadeRun.Of(NamerStandIn.Always(new NamerStandIn.Turn((["To", "bin"], "Vale"))), Flags);

        var run = await made.ReplayAsync();

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("[app] not written: no name followed the dice (K, 2, V)", run.Stderr);
        Assert.DoesNotContain("made another", run.Stderr);
        Assert.DoesNotContain("npc-writer", run.Stderr);
        Assert.Equal("", run.Stdout.Trim());
        Assert.False(File.Exists(made.SaveFile), "a run with no name saves nothing");
    }

    // A recording made in process from a stand-in, the way Program.cs would make the same calls, and
    // the save file its replay writes to. Both are temp files, removed after.
    internal sealed class MadeRun : IAsyncDisposable
    {
        public required string RecordingFile { get; init; }
        public required string SaveFile { get; init; }
        public required string[] Flags { get; init; }

        public static async Task<MadeRun> Of(NamerStandIn model, string[] flags)
        {
            var run = RunOptions.Parse(flags);
            var file = Path.Combine(Path.GetTempPath(), $"npcforge-named-{Guid.NewGuid():N}.jsonl");
            var rollSave = Path.Combine(Path.GetTempPath(), $"npcforge-named-roll-{Guid.NewGuid():N}.json");

            await using var tools = new McpToolSource(rollSave, run.Seed);
            await tools.ConnectAsync(CancellationToken.None);
            var brief = await tools.CallDirectAsync("roll_character", run.RollArguments(), CancellationToken.None);

            // The options Program.cs builds, so the replay compares like with like.
            var options = new ChatOptions { ModelId = run.Model, MaxOutputTokens = 5000 };
            var client = new RecordingChatClient(model, file, run.RecordedFlags());
            try
            {
                await ChatAgent.WriteAsync(client, options, tools, new WriterInput(brief, null, WriterInput.DefaultSkillDir), CancellationToken.None);
            }
            catch (NoNameException)
            {
                // The recording of a namer that gave no name is what the test replays.
            }

            return new MadeRun
            {
                RecordingFile = file,
                SaveFile = Path.Combine(Path.GetTempPath(), $"npcforge-named-save-{Guid.NewGuid():N}.json"),
                Flags = flags,
            };
        }

        public Task<ConsoleProcess.Result> ReplayAsync() =>
            ConsoleProcess.RunAsync(
                [.. Flags, "--provider", "replay", "--recording", RecordingFile],
                offline: true, environment: new() { ["NPCFORGE_CHARACTERS"] = SaveFile });

        public Task<ConsoleProcess.Result> LoadAsync(int number) =>
            ConsoleProcess.RunAsync(
                ["--provider", "replay", "--recording", RecordingFile, "--load", number.ToString()],
                offline: true, environment: new() { ["NPCFORGE_CHARACTERS"] = SaveFile });

        public ValueTask DisposeAsync()
        {
            File.Delete(RecordingFile);
            File.Delete(SaveFile);
            return ValueTask.CompletedTask;
        }
    }
}
