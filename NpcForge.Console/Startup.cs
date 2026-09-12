using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NpcForge;

public static class Startup
{
    public static void ConfigureServices(HostApplicationBuilder builder, string provider, string model)
    {
        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));

        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            IChatClient client = provider switch
            {
                "openai" => new OpenAI.Chat.ChatClient(
                    model,
                    RequireSecret(builder.Configuration, "OpenAI:ApiKey")).AsIChatClient(),

                "anthropic" => new AnthropicClient
                {
                    ApiKey = RequireSecret(builder.Configuration, "Anthropic:ApiKey")
                }
                .AsIChatClient(model),

                _ => throw new ArgumentException($"Unknown provider: {provider}")
            };

            // No UseFunctionInvocation here on purpose. That middleware runs the
            // tool loop for you, and build steps 2-4 are about writing it yourself.
            return new ChatClientBuilder(client)
                .UseLogging(loggerFactory)
                .Build(sp);
        });

        builder.Services.AddTransient<ChatOptions>(sp => new ChatOptions
        {
            ModelId = model,
            // No Temperature: claude-opus-5 and claude-sonnet-5 reject it with a 400.
            MaxOutputTokens = 5000
        });
    }

    private static string RequireSecret(IConfiguration config, string key) =>
        config[key] ?? throw new InvalidOperationException(
            $"Missing user secret '{key}'. Set it with: dotnet user-secrets set \"{key}\" \"<value>\"");
}
