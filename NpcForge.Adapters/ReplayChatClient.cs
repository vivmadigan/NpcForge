using Microsoft.Extensions.AI;
using System.Text.Json;                 // JsonSerializer

namespace NpcForge;

// A model that answers from a recording instead of a provider: free, offline, and the same every
// time. Each call first checks that the app sent what it sent when the recording was made, and
// stops the run if not. That stop is the regression test.
public sealed class ReplayChatClient : IChatClient
{
    private readonly List<RecordedCall> _calls = [];
    private int _next;

    public ReplayChatClient(string path)
    {
        // Line 1 is the flags, which only the test reads. Every line after it is one model call.
        foreach (var line in File.ReadLines(path).Skip(1))
            _calls.Add(JsonSerializer.Deserialize<RecordedCall>(line, Recording.Json)!);
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_next == _calls.Count)
            throw new InvalidOperationException($"The recording has {_calls.Count} model calls and the app made another.");

        var recorded = _calls[_next];
        _next++;

        // The same description of both sides, so the two strings are what you compare in the debugger.
        var expected = Recording.Describe(recorded.Request);
        var actual = Recording.Describe(RecordedRequest.From(messages, options));

        if (actual != expected)
        {
            // Name the first line that differs, so a one-word change in SKILL.md is easy to find.
            var was = expected.Split('\n');
            var now = actual.Split('\n');
            var at = 0;
            while (at < was.Length && at < now.Length && was[at] == now[at])
                at++;

            throw new InvalidOperationException(
                $"Model call {_next} differs from the recording at line {at + 1} of its description.\n" +
                $"  recorded: {(at < was.Length ? was[at] : "(nothing)")}\n" +
                $"  now:      {(at < now.Length ? now[at] : "(nothing)")}");
        }

        return Task.FromResult(recorded.Response);
    }

    // The interface has four members. Only GetResponseAsync matters here; these exist because it says so.
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
