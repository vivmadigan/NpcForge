using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;                 // Environments
using NpcForge.Api;

namespace NpcForge.Tests;

// The API in-process on a temp saves file and the stand-in model, for the tests of the forge's routes
// (ADR-019, decision 10). Keyed on a type inside the API, because the console has a Program too. A test
// that needs another provider, a seed or its own client under the forge says so in `configure`, which
// runs after these defaults and so wins.
internal sealed class ForgeHost(string savesPath, Action<IWebHostBuilder>? configure = null) : WebApplicationFactory<Forge>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production, so a fault the API does not catch reaches the test as the exception, and so no user
        // secret is loaded: a key on the owner's machine is never found from here.
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting(SavesPath.Variable, savesPath);
        builder.UseSetting(ForgeSettings.Provider, "stand-in");
        configure?.Invoke(builder);
    }
}

// Keeps what every model call was sent, as the text a replay compares (Recording.Describe), then lets
// the call through. Sits where the console's RecordingChatClient sits, under the spending cap.
internal sealed class RequestLog(IChatClient inner) : DelegatingChatClient(inner)
{
    public List<string> Requests { get; } = [];

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(Recording.Describe(RecordedRequest.From(messages, options)));
        return base.GetResponseAsync(messages, options, cancellationToken);
    }
}
