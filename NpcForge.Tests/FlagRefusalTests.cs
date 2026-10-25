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
}
