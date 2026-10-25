using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

// The Responses endpoint is still marked experimental in the OpenAI SDK (OPENAI001).
// It is used anyway: Chat Completions rejects function tools on models that reason
// by default (HTTP 400, "use /v1/responses or set reasoning_effort to 'none'").
#pragma warning disable OPENAI001

namespace NpcForge;

// Builds the IChatClient for whichever provider was asked for.
// Nothing else in the app knows which vendor is behind it.
public static class ModelClients
{
    // NPCFORGE_OFFLINE=1 is the second stop behind --provider replay and the tests' dead proxy: a
    // free run must not reach a paid model. It sits here, at construction, and not in Program.cs,
    // so it still holds when a later epic moves the model's first turn ahead of the roll. An
    // environment variable and not a flag, so a test harness or a web host can set it without
    // touching args. `offline` is for tests; null means ask the environment.
    public const string OfflineVariable = "NPCFORGE_OFFLINE";

    public static IChatClient Create(string provider, string model, IConfiguration config, string? recording = null, bool? offline = null)
    {
        // Before the switch, so before any client object exists and before any secret is read.
        if (provider is "openai" or "anthropic"
            && (offline ?? Environment.GetEnvironmentVariable(OfflineVariable) == "1"))
        {
            throw new InvalidOperationException(
                $"{OfflineVariable}=1 is set, so --provider {provider} is refused: it would call a paid model. " +
                $"Use --provider replay, or unset {OfflineVariable} for a paid run.");
        }

        return provider switch
        {
            "openai" => new ResponsesClient(RequireSecret(config, "OpenAI:ApiKey"))
                .AsIChatClient(model),

            "anthropic" => new AnthropicClient { ApiKey = RequireSecret(config, "Anthropic:ApiKey") }
                .AsIChatClient(model),

            // Free: answers from a file made with --record. No key and no network.
            "replay" => new ReplayChatClient(recording
                ?? throw new ArgumentException("--provider replay needs --recording <file>.")),

            _ => throw new ArgumentException($"Unknown provider: {provider}"),
        };
    }

    private static string RequireSecret(IConfiguration config, string key) =>
        config[key] ?? throw new InvalidOperationException(
            $"Missing user secret '{key}'. Set it with: dotnet user-secrets set \"{key}\" \"<value>\"");
}
