using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// A stand-in model for a run that names and then writes. A call whose system message is the namer's
// skill plays the next namer turn from the script; any other call is the writer's, answered at once.
// It keeps every request it was sent, so a test can read what each side was told.
internal sealed class NamerStandIn : IChatClient
{
    // One namer turn: the give_name calls it makes, or none for a turn that only replies.
    public sealed record Turn(params (string[] Syllables, string Family)[] Names);

    private readonly Queue<Turn> _script;
    private readonly Turn? _forever;

    // Plays the turns in order, then replies with no tool call.
    public NamerStandIn(params Turn[] script) => _script = new(script);

    // Plays the same turn on every namer call: a namer that never stops calling.
    public static NamerStandIn Always(Turn turn) => new(turn);
    private NamerStandIn(Turn forever) { _script = new(); _forever = forever; }

    public sealed record Request(List<ChatMessage> Messages, IReadOnlyList<string> Tools)
    {
        public string System => Messages.First(m => m.Role == ChatRole.System).Text;
        public string FirstUser => Messages.First(m => m.Role == ChatRole.User).Text;
    }

    public List<Request> NamerCalls { get; } = [];
    public List<Request> WriterCalls { get; } = [];

    public string WriterText { get; init; } = "A character.";

    public static bool IsNamer(IEnumerable<ChatMessage> messages) =>
        messages.First(m => m.Role == ChatRole.System).Text.Contains("name: namer");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = new Request([.. messages], [.. (options?.Tools ?? []).Select(t => t.Name)]);

        if (!IsNamer(messages))
        {
            WriterCalls.Add(request);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, WriterText)));
        }

        NamerCalls.Add(request);
        var turn = _forever ?? (_script.TryDequeue(out var next) ? next : new Turn());
        if (turn.Names.Length == 0)
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done.")));

        var calls = turn.Names.Select((name, i) => (AIContent)new FunctionCallContent(
            $"name-{NamerCalls.Count}-{i}", "give_name",
            new Dictionary<string, object?> { ["syllables"] = name.Syllables, ["family"] = name.Family })).ToList();
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, calls)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
