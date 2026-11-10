using System.Text.RegularExpressions;

namespace NpcForge.Tests;

// ADR-013, decisions 5 and 6, the load half, through the whole app as a process. Every run sets NPCFORGE_OFFLINE=1
// and the dead proxy (ConsoleProcess), and the provider is left at its default, openai. With the
// offline stop set, ModelClients.Create throws for that provider, so a run that succeeds here never
// built a client. That shows --load needs no API key without removing a key from the
// owner's user secrets. Each run points NPCFORGE_CHARACTERS at a temp file, never at the owner's saves.
[Trait("Category", "Process")]
public partial class LoadTests
{
    private const string OfflineMessage = "NPCFORGE_OFFLINE=1 is set";

    // A stack frame.
    [GeneratedRegex(@"^\s*at \S")]
    private static partial Regex StackFrame();

    private static Dictionary<string, string?> Saves(string file) => new() { ["NPCFORGE_CHARACTERS"] = file };

    [Fact]
    public async Task Load_of_a_number_never_saved_says_why_and_exits_1_without_a_stack_trace()
    {
        var file = SavedFile.WriteTemp();
        try
        {
            var run = await ConsoleProcess.RunAsync(["--load", "99"], offline: true, environment: Saves(file));

            Assert.Equal(1, run.ExitCode);
            Assert.Contains("[app] not loaded: ", run.Stderr);
            Assert.Contains("No character saved as 99", run.Stderr);

            // The whole of stderr: a mistyped number is the GM's slip, so nothing logs a thrown
            // exception for it (ADR-013, decision 6).
            Assert.DoesNotContain("threw an unhandled exception", run.Stderr);
            Assert.DoesNotContain(run.Stderr.Split('\n'), line => StackFrame().IsMatch(line));
            Assert.DoesNotContain("Unhandled exception", run.Stdout);
            Assert.DoesNotContain(OfflineMessage, run.Stderr);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // The same start-up as the refusal above, but the number is saved: the run finishes and prints the
    // text. Offline and openai together would refuse a client, so this also shows no client was built.
    [Fact]
    public async Task Load_of_a_saved_number_needs_no_model_client()
    {
        var file = SavedFile.WriteTemp();
        try
        {
            var run = await ConsoleProcess.RunAsync(["--load", "8"], offline: true, environment: Saves(file));

            Assert.Equal(0, run.ExitCode);
            Assert.Contains("[app] loaded 8", run.Stderr);
            Assert.Contains(SavedFile.TextMarker, run.Stdout);
            Assert.DoesNotContain(OfflineMessage, run.Stderr);
            // A load rolls nothing, so it builds no dice.
            Assert.DoesNotContain("[app] dice", run.Stderr);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // The brief line of a load reads as it did when the server sent the brief: compact, with the default
    // escaping, so it matches the run's own [app] brief line, while the file keeps the brief indented and
    // unescaped. The expected line was measured on the console before the switch to the core (ADR-016),
    // from this same file. A brief written out with GetRawText() would print the file's apostrophes,
    // letters and line breaks as they are.
    [Fact]
    public async Task Load_prints_the_brief_line_compact_and_escaped_as_before_the_switch()
    {
        var file = Path.Combine(Path.GetTempPath(), $"npcforge-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, """
            {
              "1": {
                "brief": {
                  "setting": "Gull's Rest at Ærø <stall> & \"q\" + `b` — “x” 🙂",
                  "playersWant": null,
                  "difficulty": "Wall",
                  "occupation": "smith's apprentice",
                  "tone": "Comic"
                },
                "text": "T."
              }
            }
            """);
        try
        {
            var run = await ConsoleProcess.RunAsync(["--load", "1"], offline: true, environment: Saves(file));

            Assert.True(run.ExitCode == 0, run.Stderr);
            var lines = run.Stderr.ReplaceLineEndings().Split(Environment.NewLine);
            Assert.Contains(
                """[app] brief {"setting":"Gull\u0027s Rest at \u00C6r\u00F8 \u003Cstall\u003E \u0026 \u0022q\u0022 \u002B \u0060b\u0060 \u2014 \u201Cx\u201D \uD83D\uDE42","playersWant":null,"difficulty":"Wall","occupation":"smith\u0027s apprentice","tone":"Comic"}""",
                lines);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
