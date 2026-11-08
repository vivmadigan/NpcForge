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

    // A stack frame, with or without the [server] prefix the console puts on the server's stderr.
    [GeneratedRegex(@"^(\[server\])?\s*at \S")]
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

            // The whole of stderr, the [server] lines included: the server must not log a thrown tool
            // exception for a mistyped number (ADR-013, decision 6).
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
        }
        finally
        {
            File.Delete(file);
        }
    }
}
