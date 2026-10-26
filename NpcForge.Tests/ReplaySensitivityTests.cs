using Microsoft.Extensions.AI;
using System.Text.Json;

namespace NpcForge.Tests;

// The replay is the Safety net: it stops a run that differs from the recording. A net with a hole
// in it still looks green, so each test here changes a request in exactly one way and shows the
// replay stops, and names the difference. Without them a restructure could quietly weaken
// Recording.Describe and nothing would fail. In process and free: the requests are built from
// the fixture, and the fixture is never edited or re-recorded.
//
// The writer's calls are found by their system message, not by their place (ADR-009): the namer's
// calls come first in a named run. Each test runs on the fixture as recorded and on a copy with a
// namer call in front, so a lookup by place would fail the second.
public class ReplaySensitivityTests : IDisposable
{
    private static readonly string Recorded = RecordedRun.Fixture;

    // A temp copy of the fixture with one namer call before the writer's.
    private readonly string _withNamer = RecordedRun.CopyWithNamerFirst();

    public void Dispose() => File.Delete(_withNamer);

    public static TheoryData<string> Fixtures => ["recorded", "with a namer call first"];

    private string PathOf(string fixture) => fixture == "recorded" ? Recorded : _withNamer;

    // The control. Every test below asserts a failure, so if the request built here were wrong
    // they would all pass for the wrong reason. This one shows the unmodified request replays.
    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Control_the_unmodified_requests_replay_cleanly(string fixture)
    {
        var replay = new ReplayChatClient(PathOf(fixture));

        foreach (var request in RecordedRequests(PathOf(fixture)))
            await Send(replay, request);
    }

    // The model is told the SKILL.md text, so one changed word is a changed product.
    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Fails_when_the_system_prompt_differs(string fixture)
    {
        var (replay, requests, at) = await ReplayUpToTheWriter(PathOf(fixture));
        var request = requests[at];

        var system = request.Messages.First(m => m.Role == ChatRole.System);
        var text = system.Contents.OfType<TextContent>().First();
        Assert.Contains("settled", text.Text);          // the word this test changes must exist
        text.Text = text.Text.Replace("settled", "settles");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Send(replay, request));

        Assert.Contains($"Model call {at + 1} differs", failure.Message);
        Assert.Contains("settles", failure.Message);    // names the line that changed
    }

    // "A tool the model can see is a tool it can skip" (AGENTS.md): a lost tool list must not
    // replay as if nothing changed, which is why the tools are written out by hand.
    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Fails_when_the_tool_list_is_empty(string fixture)
    {
        var (replay, requests, at) = await ReplayUpToTheWriter(PathOf(fixture));
        requests[at].Options.Tools = [];

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Send(replay, requests[at]));

        Assert.Contains($"Model call {at + 1} differs", failure.Message);
        Assert.Contains("tool: lookup_archetype", failure.Message);   // the recorded line that went missing
    }

    // Options is the library's JSON of every setting, so any one of them counts. Two are tried,
    // each on its own: a restructure that rebuilt ChatOptions might lose either.
    [Theory]
    [InlineData("MaxOutputTokens", "recorded")]
    [InlineData("ModelId", "recorded")]
    [InlineData("MaxOutputTokens", "with a namer call first")]
    [InlineData("ModelId", "with a namer call first")]
    public async Task Fails_when_a_model_option_differs(string option, string fixture)
    {
        var (replay, requests, at) = await ReplayUpToTheWriter(PathOf(fixture));
        var request = requests[at];

        if (option == "MaxOutputTokens")
            request.Options.MaxOutputTokens += 1;
        else
            request.Options.ModelId += "-other";

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Send(replay, request));

        Assert.Contains($"Model call {at + 1} differs", failure.Message);
        Assert.Contains("options:", failure.Message);
    }

    // "Skip one and the next request is malformed" (docs/architecture.md "The loop"). The drop is
    // on the writer's first request that carries the writer's own turn back, wherever it sits.
    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Fails_when_a_writer_turn_is_dropped_from_the_history(string fixture)
    {
        var path = PathOf(fixture);
        var replay = new ReplayChatClient(path);
        var requests = RecordedRequests(path);
        var at = requests.FindIndex(r => IsWriter(r) && r.Messages.Any(m =>
            m.Role == ChatRole.Assistant && m.Contents.OfType<FunctionCallContent>().Any()));
        Assert.True(at > 0, "the fixture holds no writer request that carries the writer's turn back");
        foreach (var earlier in requests.Take(at))
            await Send(replay, earlier);                 // unchanged, so the replay reaches the drop

        var withCall = requests[at].Messages.First(m =>
            m.Role == ChatRole.Assistant && m.Contents.OfType<FunctionCallContent>().Any());
        requests[at].Messages.Remove(withCall);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Send(replay, requests[at]));

        Assert.Contains($"Model call {at + 1} differs", failure.Message);
        Assert.Contains("recorded: --- assistant", failure.Message);   // the dropped turn is the line that went missing
    }

    // The writer's calls are the ones whose system message is the npc-writer skill.
    private static bool IsWriter(Request request) =>
        request.Messages.First(m => m.Role == ChatRole.System).Text.Contains("name: npc-writer");

    // Replays every request before the writer's first, unchanged, and hands back where it is.
    private static async Task<(ReplayChatClient Replay, List<Request> Requests, int At)> ReplayUpToTheWriter(string path)
    {
        var replay = new ReplayChatClient(path);
        var requests = RecordedRequests(path);
        var at = requests.FindIndex(IsWriter);
        Assert.True(at >= 0, "the fixture holds no writer call");
        foreach (var earlier in requests.Take(at))
            await Send(replay, earlier);
        return (replay, requests, at);
    }

    // What the app would hand the model, rebuilt from the fixture: the messages, and a ChatOptions
    // with the recorded settings and tools. Each call gets fresh objects, so a test can change them.
    private static List<Request> RecordedRequests(string path)
    {
        var requests = new List<Request>();

        foreach (var line in File.ReadLines(path).Skip(1))
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
