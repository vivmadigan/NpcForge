namespace NpcForge.Tests;

// The lab's flags and every refusal of ADR-009 section 5, free and in process. Each refusal stops
// the run before a model client is built, so each test names the whole message and not just a
// failure: two refusals that shared a message could hide each other (lesson 004).
public class LabFlagTests
{
    // A lab that is good on every count. Each test below breaks exactly one thing.
    private static readonly string[] Good =
        ["--lab", "skill=skills/a,skills/b", "--seed", "1", "--setting", "a farm"];

    private static string[] Without(params string[] flags) => [.. Good.Except(flags)];

    private static string Refusal(params string[] args) =>
        Assert.Throws<ArgumentException>(() => RunOptions.Parse(args)).Message;

    [Fact]
    public void A_good_lab_parses_with_its_defaults()
    {
        var run = RunOptions.Parse(Good);

        Assert.Equal("skill", run.Lab!.Axis);
        Assert.Equal(["a", "b"], run.Lab.Variants.Select(v => v.Label));
        Assert.Equal(1, run.Repeat);
        Assert.Equal("side", run.Mode);
        Assert.Null(run.Out);
        Assert.False(run.Interview);
    }

    [Fact]
    public void The_lab_flags_are_not_answers_and_not_recorded()
    {
        var run = RunOptions.Parse([.. Good, "--repeat", "2", "--mode", "blind", "--out", "Lab Out"]);

        Assert.Equal(4, run.RollArguments().Count);
        Assert.DoesNotContain("--lab", run.RecordedFlags());
        Assert.DoesNotContain("--repeat", run.RecordedFlags());
        // Folders are read as typed, not lower-cased.
        Assert.Equal("Lab Out", run.Out);
        Assert.Equal("blind", run.Mode);
    }

    [Fact]
    public void A_skill_folder_keeps_its_case()
    {
        var run = RunOptions.Parse(["--lab", "skill=Skills/Gentle,Skills/Terse", "--seed", "1", "--setting", "a farm"]);

        Assert.Equal(["Gentle", "Terse"], run.Lab!.Variants.Select(v => v.Label));
    }

    [Fact]
    public void A_lab_without_a_seed_is_refused()
    {
        Assert.Equal(
            "--lab needs --seed, for example --seed 1: every variant is written from the same brief.",
            Refusal(Without("--seed", "1")));
    }

    [Fact]
    public void A_lab_without_an_answer_flag_is_refused()
    {
        Assert.Equal(
            "--lab needs the answers as flags (--setting, --want, --difficulty or --occupation): " +
            "an interview cannot be repeated, and a lab of a typed scene waits for a later epic.",
            Refusal(Without("--setting", "a farm")));
    }

    [Fact]
    public void A_lab_with_load_is_refused()
    {
        Assert.Equal(
            "--lab cannot go with --load: a lab writes new characters and loads none.",
            Refusal([.. Good, "--load", "3"]));
    }

    [Fact]
    public void A_lab_with_record_is_refused()
    {
        Assert.Equal(
            "--lab cannot go with --record: a lab is compared on its sheet and is never recorded.",
            Refusal([.. Good, "--record", "r.jsonl"]));
    }

    [Fact]
    public void A_lab_with_the_replay_provider_is_refused()
    {
        Assert.Equal(
            "--lab cannot go with --provider replay: a replay answers from one recording, so every variant would read the same.",
            Refusal([.. Good, "--provider", "replay", "--recording", "r.jsonl"]));
    }

    [Theory]
    [InlineData("--repeat", "2")]
    [InlineData("--mode", "blind")]
    [InlineData("--out", "somewhere")]
    public void A_lab_flag_without_lab_is_refused_by_name(string flag, string value)
    {
        Assert.Equal($"{flag} only goes with --lab.", Refusal(flag, value, "--seed", "1"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void A_repeat_below_one_is_refused(string repeat)
    {
        Assert.Equal("--repeat needs a number of 1 or more.", Refusal([.. Good, "--repeat", repeat]));
    }

    [Fact]
    public void A_mode_that_is_not_side_or_blind_is_refused()
    {
        Assert.Equal("--mode is side or blind, not 'both'.", Refusal([.. Good, "--mode", "both"]));
    }

    [Fact]
    public void The_same_skill_folder_twice_is_refused_even_spelled_two_ways()
    {
        var message = Refusal("--lab", "skill=skills/a,./skills/a/", "--seed", "1", "--setting", "a farm");

        Assert.Equal(
            $"--lab skill names the folder {Path.GetFullPath("skills/a").TrimEnd(Path.DirectorySeparatorChar)} twice: " +
            "each variant needs its own skill folder.",
            message);
    }

    [Theory]
    [InlineData("skill=skills/a", "skill", "skill=skills/a,skills/b")]
    [InlineData("skill=", "skill", "skill=skills/a,skills/b")]
    [InlineData("skill", "skill", "skill=skills/a,skills/b")]
    [InlineData("temperature=0.5", "temperature", "temperature=0.2,0.9")]
    [InlineData("temperature=0.5,", "temperature", "temperature=0.2,0.9")]
    public void Fewer_than_two_values_on_the_axis_is_refused(string lab, string axis, string example)
    {
        Assert.Equal(
            $"--lab {axis} needs at least two values to compare, for example --lab {example}.",
            Refusal("--lab", lab, "--seed", "1", "--setting", "a farm"));
    }

    [Fact]
    public void A_temperature_that_is_not_a_number_is_refused_by_value()
    {
        Assert.Equal(
            "--lab temperature needs numbers of 0 or more, and 'hot' is not one.",
            Refusal("--lab", "temperature=0.2,hot", "--seed", "1", "--setting", "a farm"));
    }

    [Fact]
    public void The_tone_axis_is_refused_until_tone_exists()
    {
        Assert.Equal(
            "--lab tone is not built yet: the tone axis arrives with tone itself (ADR-009, PR 7). " +
            "The axes that exist now are: skill, temperature.",
            Refusal("--lab", "tone", "--seed", "1", "--setting", "a farm"));
    }

    [Fact]
    public void An_unknown_axis_lists_the_two_that_exist()
    {
        Assert.Equal(
            "Unknown lab axis 'model'. The axes are: skill, temperature.",
            Refusal("--lab", "model=a,b", "--seed", "1", "--setting", "a farm"));
    }

    [Fact]
    public void A_temperature_lab_with_the_anthropic_provider_is_refused()
    {
        Assert.Equal(
            "--lab temperature cannot go with --provider anthropic: its models reject a temperature with a 400.",
            Refusal("--lab", "temperature=0.2,0.9", "--seed", "1", "--setting", "a farm", "--provider", "anthropic"));
    }

    // The same pair of folders is fine on anthropic: only a temperature is rejected there.
    [Fact]
    public void A_skill_lab_with_the_anthropic_provider_is_allowed()
    {
        var run = RunOptions.Parse([.. Good, "--provider", "anthropic"]);

        Assert.Equal("anthropic", run.Provider);
    }

    [Fact]
    public void A_blind_lab_of_more_than_26_characters_is_refused()
    {
        Assert.Equal(
            "--mode blind letters its characters A to Z, so it writes at most 26: 2 variants x 14 takes is too many.",
            Refusal([.. Good, "--mode", "blind", "--repeat", "14"]));
        Assert.Equal("blind", RunOptions.Parse([.. Good, "--mode", "blind", "--repeat", "13"]).Mode);
    }

    [Theory]
    [InlineData("--seed", "1")]
    [InlineData("--setting", "a farm")]
    [InlineData("--provider", "replay")]
    [InlineData("--lab", "skill=a,b")]
    public void Score_with_any_other_flag_is_refused_by_name(string flag, string value)
    {
        Assert.Equal(
            $"--score takes no other flag, and {flag} was given: it reads a finished blind lab and calls no model.",
            Refusal("--score", "lab-1", flag, value));
    }

    [Fact]
    public void Score_alone_parses_and_is_no_lab()
    {
        var run = RunOptions.Parse(["--score", "lab-1"]);

        Assert.Equal("lab-1", run.Score);
        Assert.Null(run.Lab);
    }

    // Which file a lab saves in: never the tracked one, whatever the lab does (the second stop).
    [Fact]
    public void A_lab_saves_to_a_fresh_temp_file()
    {
        var run = RunOptions.Parse(Good);

        var path = run.CharactersPath(null);

        Assert.NotNull(path);
        Assert.StartsWith(Path.GetTempPath(), path);
        Assert.Contains("npcforge-lab-", path);
        Assert.NotEqual(path, run.CharactersPath(null));
    }

    [Fact]
    public void A_file_the_caller_named_wins_for_a_lab_too()
    {
        Assert.Equal(@"C:\mine\saves.json", RunOptions.Parse(Good).CharactersPath(@"C:\mine\saves.json"));
    }
}

// The same refusals through the whole app as a process, with both independent stops set (offline
// and the dead proxy): the message is the parser's, not the offline stop's and not a missing key's.
public class LabRefusalProcessTests
{
    [Theory]
    [InlineData(new[] { "--lab", "skill=a,b", "--setting", "a farm" }, "--lab needs --seed")]
    [InlineData(new[] { "--lab", "skill=a,b", "--seed", "1" }, "--lab needs the answers as flags")]
    [InlineData(new[] { "--lab", "tone", "--seed", "1", "--setting", "a farm" }, "the tone axis arrives with tone itself")]
    public async Task The_app_stops_a_bad_lab_at_the_parser_before_any_client_is_built(string[] flags, string message)
    {
        var run = await ConsoleProcess.RunAsync(["--provider", "openai", .. flags], offline: true);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(message, run.Stderr);
        Assert.DoesNotContain("NPCFORGE_OFFLINE=1 is set", run.Stderr);
        Assert.DoesNotContain("Missing user secret", run.Stderr);
        Assert.DoesNotContain("[loop]", run.Stderr);
    }
}
