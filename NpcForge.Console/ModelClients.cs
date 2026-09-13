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
    public static IChatClient Create(string provider, string model, IConfiguration config) => provider switch
    {
        "openai" => new ResponsesClient(RequireSecret(config, "OpenAI:ApiKey"))
            .AsIChatClient(model),

        "anthropic" => new AnthropicClient { ApiKey = RequireSecret(config, "Anthropic:ApiKey") }
            .AsIChatClient(model),

        _ => throw new ArgumentException($"Unknown provider: {provider}"),
    };

    private static string RequireSecret(IConfiguration config, string key) =>
        config[key] ?? throw new InvalidOperationException(
            $"Missing user secret '{key}'. Set it with: dotnet user-secrets set \"{key}\" \"<value>\"");
}
