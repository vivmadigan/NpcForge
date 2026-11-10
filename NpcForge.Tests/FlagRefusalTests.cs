namespace NpcForge.Tests;

// The refusal in RunOptions.Parse, through the whole app as a process: a typo'd flag, or a bad lab,
// next to the default paid provider must stop at the parser. Both independent stops are set,
// NPCFORGE_OFFLINE=1 and the dead proxy, so a parser that stopped refusing would fail for free. The
// test asserts the parser's message and not the offline one, which is what shows the parser did the
// stopping (lesson 004). RunOptionsTests and LabFlagTests hold every refusal's message in process.
[Trait("Category", "Process")]
public class FlagRefusalTests
{
    [Theory]
    [InlineData(new[] { "--provider", "openai", "--setings", "farm" }, "Unknown flag --setings.")]
    [InlineData(new[] { "--provider", "openai", "--setting" }, "--setting needs a value after it.")]
    [InlineData(new[] { "--provider", "openai", "--lab", "skill=a,b", "--setting", "a farm" }, "--lab needs --seed")]
    public async Task The_app_stops_at_the_parser_before_any_client_is_built(string[] flags, string message)
    {
        var run = await ConsoleProcess.RunAsync(flags, offline: true);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(message, run.Stderr);
        Assert.DoesNotContain("NPCFORGE_OFFLINE=1 is set", run.Stderr);
        Assert.DoesNotContain("Missing user secret", run.Stderr);
        Assert.DoesNotContain("[loop]", run.Stderr);
    }

    // ADR-009: the parser takes any text for --tone, so the roll is what refuses 7. The run stops
    // there, by the field's name, before the writer is called. The replay provider and the dead proxy keep
    // it free; the recording is never read, because no model call is made.
    [Theory]
    [InlineData("--tone", "7", "tone 7")]
    [InlineData("--letter", "99", "nameLetter 99")]
    [InlineData("--syllables", "7", "nameSyllables 7")]
    [InlineData("--family-letter", "99", "familyLetter 99")]
    [InlineData("--difficulty", "7", "difficulty 7")]
    public async Task The_app_stops_at_the_roll_on_an_undefined_enum_value_and_names_the_field(string flag, string value, string reason)
    {
        var fixture = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "replay.jsonl");

        var run = await ConsoleProcess.RunAsync(["--provider", "replay", "--recording", fixture, flag, value], offline: true);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains($"{reason} is not one of its values", run.Stderr);
        Assert.DoesNotContain("[loop]", run.Stderr);
        Assert.DoesNotContain("[app] brief", run.Stderr);
    }

    // ADR-016: a choice the field does not have ends the run with one line that names the field and its
    // values, exit code 1 and no stack trace. Before, --tone grumpy ended in an unhandled exception.
    // Program.cs catches the refusal once, around the lab too; LabRunTests holds that the lab lets it out.
    [Fact]
    public async Task A_choice_the_roll_does_not_have_stops_the_run_with_one_line_and_exit_1()
    {
        var run = await ConsoleProcess.RunAsync(
            ["--provider", "replay", "--recording", RecordedRun.Fixture, "--tone", "grumpy"], offline: true);

        Assert.Equal(1, run.ExitCode);
        var lines = run.Stderr.ReplaceLineEndings().Split(Environment.NewLine).ToList();
        Assert.Contains("[app] not rolled: tone grumpy is not one of its values: Serious, Comic, Heartfelt.", lines);
        Assert.DoesNotContain("Unhandled exception", run.Stderr);
        Assert.DoesNotContain(lines, line => line.TrimStart().StartsWith("at "));
        Assert.DoesNotContain("[loop]", run.Stderr);
        Assert.DoesNotContain("[app] brief", run.Stderr);
    }
}
