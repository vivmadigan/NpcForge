using Microsoft.Extensions.AI;

namespace NpcForge;

// What every model call of a run is sent besides its messages. One place builds it, so the console
// and the website's API cannot drift apart, and a recording made from one replays against the other.
public static class ModelOptions
{
    // Shared by every request. Step 4 fills in Tools from the tool source before the first turn.
    public static ChatOptions New(string modelId) => new()
    {
        ModelId = modelId,
        // No Temperature: claude-opus-5 and claude-sonnet-5 reject it with a 400.
        MaxOutputTokens = 5000,
    };
}
