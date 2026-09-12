using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace NpcForge;

// Builds the IChatClient for whichever provider was asked for.
// Nothing else in the app knows which vendor is behind it.
public static class ModelClients
{
    public static IChatClient Create(string provider, string model, IConfiguration config) => provider switch
    {
        "openai" => new OpenAI.Chat.ChatClient(model, RequireSecret(config, "OpenAI:ApiKey"))
            .AsIChatClient(),

        "anthropic" => new AnthropicClient { ApiKey = RequireSecret(config, "Anthropic:ApiKey") }
            .AsIChatClient(model),

        _ => throw new ArgumentException($"Unknown provider: {provider}"),
    };

    private static string RequireSecret(IConfiguration config, string key) =>
        config[key] ?? throw new InvalidOperationException(
            $"Missing user secret '{key}'. Set it with: dotnet user-secrets set \"{key}\" \"<value>\"");
}
