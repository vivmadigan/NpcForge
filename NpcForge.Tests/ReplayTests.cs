using Microsoft.Extensions.AI;
using System.Diagnostics;               // Process, ProcessStartInfo
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
        // Four folders up from this test's bin is the repo, as McpToolSource finds the server.
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
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

        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(Path.Combine(repo, "NpcForge.Console"));
        start.ArgumentList.Add("--no-build");
        start.ArgumentList.Add("--no-launch-profile");
        start.ArgumentList.Add("--");
        foreach (var flag in flags)
            start.ArgumentList.Add(flag);
        start.ArgumentList.Add("--provider");
        start.ArgumentList.Add("replay");
        start.ArgumentList.Add("--recording");
        start.ArgumentList.Add(fixture);

        // The console never names a save file, so the server inherits this one from it.
        start.Environment["NPCFORGE_CHARACTERS"] = saveFile;

        // Free by construction, not by one flag. The child console can read the owner's real API
        // keys from its user secrets. If a later change to flag handling ever dropped
        // --provider replay, the run would be a paid one. A proxy that nothing listens on makes it
        // fail at the proxy instead, and the telemetry opt-out stops the dotnet CLI itself from
        // sending anything through it.
        start.Environment["HTTPS_PROXY"] = "http://127.0.0.1:9";
        start.Environment["HTTP_PROXY"] = "http://127.0.0.1:9";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        try
        {
            using var process = Process.Start(start)!;

            // Read both streams while it runs. Left unread, the [server] and [loop] lines fill
            // the pipe and the app stops, waiting for someone to read them.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            // A minute is far more than a replay needs. A hang fails the test instead of the test
            // run never ending.
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            // On failure, the app's own trace is the message: the replay names the line that differs.
            Assert.True(process.ExitCode == 0, await stderr);

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
