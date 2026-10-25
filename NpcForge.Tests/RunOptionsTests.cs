using System.Text.Json;

namespace NpcForge.Tests;

// The flag parser, free and in process. The refusals stop a run before any model client is built,
// so each test names the message, not just a failure (lesson 004).
public class RunOptionsTests
{
    private static readonly string Fixture = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "NpcForge.Tests", "Fixtures", "replay.jsonl"));

    [Fact]
    public void No_args_gives_the_defaults()
    {
        var run = RunOptions.Parse([]);

        Assert.Equal("openai", run.Provider);
        Assert.Equal("gpt-6-luna", run.Model);
        Assert.Equal("an inn in a major city", run.Setting);
        Assert.Equal("the name of a fence", run.Want);
        Assert.Equal("Wall", run.Difficulty);
        Assert.Equal("innkeeper", run.Occupation);
        Assert.Null(run.Seed);
        Assert.Null(run.Load);
        Assert.Null(run.RecordTo);
        Assert.Null(run.Recording);
    }

    [Fact]
    public void Each_flag_sets_its_own_value()
    {
        var run = RunOptions.Parse(
        [
            "--provider", "Replay", "--model", "Claude-Opus-5",
            "--setting", "a farm", "--want", "a cart", "--difficulty", "Hill", "--occupation", "farmer",
            "--seed", "7", "--load", "3", "--recording", "r.jsonl",
        ]);

        Assert.Equal("replay", run.Provider);
        Assert.Equal("claude-opus-5", run.Model);
        Assert.Equal("a farm", run.Setting);
        Assert.Equal("a cart", run.Want);
        Assert.Equal("Hill", run.Difficulty);
        Assert.Equal("farmer", run.Occupation);
        Assert.Equal(7, run.Seed);
        Assert.Equal(3, run.Load);
        Assert.Equal("r.jsonl", run.Recording);
    }

    [Fact]
    public void Record_sets_the_recording_file()
    {
        var run = RunOptions.Parse(["--seed", "1", "--record", "out.jsonl"]);

        Assert.Equal("out.jsonl", run.RecordTo);
    }

    private const string Known =
        "The flags are: --provider, --seed, --model, --setting, --want, --difficulty, --occupation, --load, --record, --recording.";

    [Fact]
    public void An_unknown_flag_stops_the_run_and_names_the_flags_the_app_knows()
    {
        var error = Assert.Throws<ArgumentException>(() => RunOptions.Parse(["--setings", "farm"]));

        Assert.Equal($"Unknown flag --setings. {Known}", error.Message);
    }

    [Fact]
    public void A_stray_word_stops_the_run()
    {
        var error = Assert.Throws<ArgumentException>(() => RunOptions.Parse(["--setting", "a farm", "farm"]));

        Assert.Equal(
            $"Unexpected argument 'farm': a flag starts with -- and is followed by its value. {Known}",
            error.Message);
    }

    // The value is consumed with its flag, so `--setting --load 3` no longer sets the setting to
    // "--load" and loads 3 as well: --setting has no value.
    [Fact]
    public void A_flag_followed_by_a_flag_has_no_value()
    {
        var error = Assert.Throws<ArgumentException>(() => RunOptions.Parse(["--setting", "--load", "3"]));

        Assert.Equal("--setting needs a value after it.", error.Message);
    }

    [Theory]
    [InlineData("--provider", "--provider needs a value after it.")]
    [InlineData("--seed", "--seed needs a number, for example --seed 1.")]
    [InlineData("--model", "--model needs a value after it.")]
    [InlineData("--setting", "--setting needs a value after it.")]
    [InlineData("--want", "--want needs a value after it.")]
    [InlineData("--difficulty", "--difficulty needs a value after it.")]
    [InlineData("--occupation", "--occupation needs a value after it.")]
    [InlineData("--load", "--load needs a number, for example --load 3.")]
    [InlineData("--record", "--record needs a file, for example --record NpcForge.Tests/Fixtures/replay.jsonl.")]
    [InlineData("--recording", "--recording needs a file, for example --recording NpcForge.Tests/Fixtures/replay.jsonl.")]
    public void Every_flag_given_last_stops_the_run(string flag, string message)
    {
        var error = Assert.Throws<ArgumentException>(() => RunOptions.Parse([flag]));

        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData("--provider")]
    [InlineData("--seed")]
    [InlineData("--model")]
    [InlineData("--setting")]
    [InlineData("--want")]
    [InlineData("--difficulty")]
    [InlineData("--occupation")]
    [InlineData("--load")]
    [InlineData("--record")]
    [InlineData("--recording")]
    public void Every_flag_followed_by_a_flag_gets_the_same_message_as_when_last(string flag)
    {
        var last = Assert.Throws<ArgumentException>(() => RunOptions.Parse([flag]));
        var beforeAnother = Assert.Throws<ArgumentException>(() => RunOptions.Parse([flag, "--difficulty", "Hill"]));

        Assert.Equal(last.Message, beforeAnother.Message);
    }

    [Theory]
    [InlineData(new[] { "--load" }, "--load needs a number, for example --load 3.")]
    [InlineData(new[] { "--record" }, "--record needs a file, for example --record NpcForge.Tests/Fixtures/replay.jsonl.")]
    [InlineData(new[] { "--record", "f.jsonl" }, "--record needs --seed, for example --seed 1.")]
    [InlineData(new[] { "--recording", "f.jsonl" }, "--recording only goes with --provider replay.")]
    [InlineData(new[] { "--seed", "1", "--record", "f.jsonl", "--provider", "replay" },
        "--record cannot go with --provider replay: a replay never writes a recording.")]
    public void The_existing_refusals_still_stop_the_run(string[] args, string message)
    {
        var error = Assert.Throws<ArgumentException>(() => RunOptions.Parse(args));

        Assert.Equal(message, error.Message);
    }

    // Line 1 of a recording is built from the same table the parser reads, so the fixture's own
    // line 1, parsed and written back, is exactly the fixture's line 1.
    [Fact]
    public void Line_one_built_from_the_fixtures_flags_is_the_fixtures_line_one()
    {
        var line1 = File.ReadLines(Fixture).First();
        var flags = JsonSerializer.Deserialize<string[]>(line1)!;

        var written = RunOptions.Parse(flags).RecordedFlags();

        Assert.Equal(line1, JsonSerializer.Serialize(written, NpcForge.Recording.Json));
    }

    // The value used, not the text typed: a default fills in what the command line left out.
    [Fact]
    public void Line_one_holds_the_defaults_for_answers_not_given()
    {
        var written = RunOptions.Parse(["--seed", "01", "--model", "GPT-6-Luna"]).RecordedFlags();

        Assert.Equal(
            ["--seed", "1", "--model", "gpt-6-luna",
             "--setting", "an inn in a major city", "--want", "the name of a fence",
             "--difficulty", "Wall", "--occupation", "innkeeper"],
            written);
    }
}
