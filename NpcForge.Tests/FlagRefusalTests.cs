namespace NpcForge.Tests;

// The refusal in RunOptions.Parse, through the whole app as a process: a typo'd flag next to the
// default paid provider must stop at the parser. Both independent stops are set, NPCFORGE_OFFLINE=1
// and the dead proxy, so a parser that stopped refusing would fail for free. The test asserts the
// parser's message and not the offline one, which is what shows the parser did the stopping
// (lesson 004).
public class FlagRefusalTests
{
    [Theory]
    [InlineData(new[] { "--provider", "openai", "--setings", "farm" }, "Unknown flag --setings.")]
    [InlineData(new[] { "--provider", "openai", "--setting" }, "--setting needs a value after it.")]
    public async Task The_app_stops_at_the_parser_before_any_client_is_built(string[] flags, string message)
    {
        var run = await ConsoleProcess.RunAsync(flags, offline: true);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(message, run.Stderr);
        Assert.DoesNotContain("NPCFORGE_OFFLINE=1 is set", run.Stderr);
        Assert.DoesNotContain("Missing user secret", run.Stderr);
        Assert.DoesNotContain("[loop]", run.Stderr);
    }

    // ADR-009: the parser takes any text for --tone, so the server's roll is what refuses 7. The run stops
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
}
