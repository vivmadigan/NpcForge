using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Nodes;           // JsonNode

namespace NpcForge.Tests;

// The whole app, run as a process against a recorded model: free, and no network. A black box on
// purpose, so it still holds when the "Ready to grow" epic moves every class in the console. It
// checks the save file, not what was printed: the save is exact, and a child process's stdout is
// not (lesson 006).
public class ReplayTests
{
    [Fact]
    public async Task Replays_the_recorded_run_and_saves_the_same_character()
    {
        var repo = ConsoleProcess.RepoRoot;
        var fixture = Path.Combine(repo, "NpcForge.Tests", "Fixtures", "replay.jsonl");
        var saveFile = Path.Combine(Path.GetTempPath(), $"npcforge-replay-{Guid.NewGuid():N}.json");

        // Line 1 is the flags the run was recorded with. The test adds the replay provider.
        // Every line after it is one model call.
        var lines = File.ReadAllLines(fixture);
        var flags = JsonSerializer.Deserialize<string[]>(lines[0])!;
        var calls = lines.Skip(1).Select(line => JsonNode.Parse(line)!).ToList();

        // Before the app runs, and from the fixture alone: a recording that guards nothing must
        // fail here, not pass.
        RequireRecordingThatGuardsSomething(calls);

        try
        {
            // The console never names a save file, so the server inherits this one from it.
            var run = await ConsoleProcess.RunAsync(
                [.. flags, "--provider", "replay", "--recording", fixture],
                environment: new() { ["NPCFORGE_CHARACTERS"] = saveFile });

            // On failure, the app's own trace is the message: the replay names the line that differs.
            Assert.True(run.ExitCode == 0, run.Stderr);

            // What the recorded run saved: the text of the model's last reply, and the brief from
            // the first message. Read with the library's own types, not the console's, so a
            // restructure can move every console class without touching this test.
            var lastReply = Reply(calls[^1]);

            var userText = Messages(calls[0]).First(m => m.Role == ChatRole.User).Text;
            var recordedBrief = JsonNode.Parse(userText[userText.IndexOf('{')..]);   // the brief is the JSON at its end

            var saved = JsonNode.Parse(File.ReadAllText(saveFile))!["1"]!;

            Assert.Equal(lastReply.Text, saved["text"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(recordedBrief, saved["brief"]), $"Saved brief: {saved["brief"]}");
        }
        finally
        {
            File.Delete(saveFile);
        }
    }

    // The replay only catches what the recording makes visible. Three things it needs, each of
    // which a thin recording lacks without anyone noticing.
    private static void RequireRecordingThatGuardsSomething(List<JsonNode> calls)
    {
        const string reRecord = " Re-record it with the command in docs/runs/README.md.";

        // One call never sends anything of the model's own back, so there is no history to damage.
        Assert.True(calls.Count >= 2,
            $"The recording holds {calls.Count} model call(s); it needs at least 2.{reRecord}");

        // Without a function call from the model in a later request, dropping the model's turn
        // from the history changes nothing the replay can see.
        var sendsBackAToolCall = calls.Skip(1).Any(call => Messages(call).Any(message =>
            message.Role == ChatRole.Assistant && message.Contents.OfType<FunctionCallContent>().Any()));
        Assert.True(sendsBackAToolCall,
            $"No request after the first holds an assistant function call, so dropping the model's turn from the history would go unseen.{reRecord}");

        // The test compares the saved text with the last reply. An empty one compares nothing.
        Assert.False(string.IsNullOrWhiteSpace(Reply(calls[^1]).Text),
            $"The last response has no text, so the saved text is not checked.{reRecord}");
    }

    // Read with the library's own types, as the console wrote them.
    private static List<ChatMessage> Messages(JsonNode call) =>
        call["request"]!["messages"].Deserialize<List<ChatMessage>>(AIJsonUtilities.DefaultOptions)!;

    private static ChatResponse Reply(JsonNode call) =>
        call["response"].Deserialize<ChatResponse>(AIJsonUtilities.DefaultOptions)!;
}
