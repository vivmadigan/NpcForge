using Microsoft.Extensions.AI;

namespace NpcForge;

// Stops one session running up a bill (ADR-007, "Spending"). It wraps the model client once, so the
// interview and the writer draw on the same allowance without either loop knowing. It counts the
// tokens each response reports and refuses the next call once the total reaches the cap, before the
// model is asked. One call can overshoot by its own size; the cap stops the one after it.
// Below the cap it changes nothing, so a replay sees the same calls as before.
public sealed class SpendingCap(IChatClient inner, long maxTokens) : DelegatingChatClient(inner)
{
    // A first guess, sized for one interview and one writer run with room for retries.
    public const long TokensPerSession = 200_000;

    public long Spent { get; private set; }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (Spent >= maxTokens)
            throw new SpendingCapReachedException(Spent, maxTokens);

        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        // Usage is what both vendors fill. A response with none counts as nothing, which a stand-in
        // model may do; a real provider always reports it.
        Spent += response.Usage?.TotalTokenCount
            ?? (response.Usage?.InputTokenCount ?? 0) + (response.Usage?.OutputTokenCount ?? 0);
        return response;
    }

    // The app never streams. A stream would pass the cap uncounted, so it is refused, not let through.
    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("SpendingCap counts whole responses only; the app never streams.");
}

public sealed class SpendingCapReachedException(long spent, long cap)
    : InvalidOperationException($"Spending cap reached: this session has used {spent:N0} tokens of {cap:N0}. Nothing more is sent to the model.");
