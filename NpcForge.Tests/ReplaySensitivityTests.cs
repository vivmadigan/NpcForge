using Microsoft.Extensions.AI;
using System.Text.Json;

namespace NpcForge.Tests;

// The replay is the Safety net: it stops a run that differs from the recording. A net with a hole
// in it still looks green, so each test here changes a request in exactly one way and shows the
// replay stops, and names the difference. Without them a restructure could quietly weaken
// Recording.Describe and nothing would fail. In process and free: the requests are built from
// the fixture, and the fixture is never edited or re-recorded.
public class ReplaySensitivityTests
{
    private static readonly string Fixture = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "NpcForge.Tests", "Fixtures", "replay.jsonl"));

    // The control. Every test below asserts a failure, so if the request built here were wrong
    // they would all pass for the wrong reason. This one shows the unmodified request replays.
    [Fact]
    public async Task Control_the_unmodified_requests_replay_cleanly()
    {
        var replay = new ReplayChatClient(Fixture);

        foreach (var request in RecordedRequests())
            await Send(replay, request);
    }

    // The model is told the SKILL.md text, so one changed word is a changed product.
    [Fact]
    public async Task Fails_when_the_system_prompt_differs()
    {
        var replay = new ReplayChatClient(Fixture);
        var request = RecordedRequests()[0];

        var system = request.Messages.First(m => m.Role == ChatRole.System);
        var text = system.Contents.OfType<TextContent>().First();
        Assert.Contains("settled", text.Text);          // the word this test changes must exist
        text.Text = text.Text.Replace("settled", "settles");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Send(replay, request));

        Assert.Contains("Model call 1 differs", failure.Message);
        Assert.Contains("settles", failure.Message);    // names the line that changed
    }

    // "A tool the model can see is a tool it can skip" (AGENTS.md): a lost tool list must not
    // replay as if nothing changed, which is why the tools are written out by hand.
    [Fact]
    public async Task Fails_when_the_tool_list_is_empty()
    {
        var replay = new ReplayChatClient(Fixture);
        var request = RecordedRequests()[0];
        request.Options.Tools = [];

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Send(replay, request));

        Assert.Contains("Model call 1 differs", failure.Message);
        Assert.Contains("tool: lookup_archetype", failure.Message);   // the recorded line that went missing
    }

    // Options is the library's JSON of every setting, so any one of them counts. Two are tried,
    // each on its own: a restructure that rebuilt ChatOptions might lose either.
    [Theory]
    [InlineData("MaxOutputTokens")]
    [InlineData("ModelId")]
    public async Task Fails_when_a_model_option_differs(string option)
    {
        var replay = new ReplayChatClient(Fixture);
        var request = RecordedRequests()[0];

        if (option == "MaxOutputTokens")
            request.Options.MaxOutputTokens += 1;
        else
            request.Options.ModelId += "-other";

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Send(replay, request));

        Assert.Contains("Model call 1 differs", failure.Message);
        Assert.Contains("options:", failure.Message);
    }

    // "Skip one and the next request is malformed" (docs/architecture.md "The loop"). The drop
    // is on call 2, the first request that carries the model's own turn back.
    [Fact]
    public async Task Fails_when_a_model_turn_is_dropped_from_the_history()
    {
        var replay = new ReplayChatClient(Fixture);
        var requests = RecordedRequests();
        await Send(replay, requests[0]);                 // call 1 is unchanged, so the replay reaches call 2

        var withCall = requests[1].Messages.First(m =>
            m.Role == ChatRole.Assistant && m.Contents.OfType<FunctionCallContent>().Any());
        requests[1].Messages.Remove(withCall);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Send(replay, requests[1]));

        Assert.Contains("Model call 2 differs", failure.Message);
        Assert.Contains("recorded: --- assistant", failure.Message);   // the dropped turn is the line that went missing
    }

    // What the app would hand the model, rebuilt from the fixture: the messages, and a ChatOptions
    // with the recorded settings and tools. Each call gets fresh objects, so a test can change them.
    private static List<Request> RecordedRequests()
    {
        var requests = new List<Request>();

        foreach (var line in File.ReadLines(Fixture).Skip(1))
        {
            var call = JsonSerializer.Deserialize<RecordedCall>(line, Recording.Json)!;
            var options = new ChatOptions
            {
                MaxOutputTokens = call.Request.Options.GetProperty("maxOutputTokens").GetInt32(),
                ModelId = call.Request.Options.GetProperty("modelId").GetString(),
                Tools = [.. call.Request.Tools.Select(t =>
                    (AITool)AIFunctionFactory.CreateDeclaration(t.Name, t.Description, t.Schema))],
            };
            requests.Add(new Request(call.Request.Messages, options));
        }

        return requests;
    }

    private static Task<ChatResponse> Send(ReplayChatClient replay, Request request) =>
        replay.GetResponseAsync(request.Messages, request.Options);

    private sealed record Request(List<ChatMessage> Messages, ChatOptions Options);
}
