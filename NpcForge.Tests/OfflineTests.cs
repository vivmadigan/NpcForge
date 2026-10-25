using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;           // JsonNode

namespace NpcForge.Tests;

// NPCFORGE_OFFLINE=1 is the second, independent stop behind the replay test's dead proxy: it
// refuses the paid providers when the client is built. Both layers run here against the real
// ModelClients, with a dummy key set so a missing key cannot be the reason a run fails, and with
// the dead proxy as the backstop so a refusal that stopped working fails at the proxy and never
// pays. Each assertion names the offline message, not just a failure (lesson 004).
public class OfflineTests
{
    private const string OfflineMessage = "NPCFORGE_OFFLINE=1 is set";

    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    public void Create_refuses_a_paid_provider_when_offline_even_with_a_key_set(string provider)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "dummy-not-a-real-key",
                ["Anthropic:ApiKey"] = "dummy-not-a-real-key",
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(
            () => ModelClients.Create(provider, "any-model", config, offline: true));

        Assert.Contains(OfflineMessage, error.Message);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    public void Create_refuses_before_it_reads_a_secret(string provider)
    {
        // No key at all. Without the offline stop, the error would be "Missing user secret".
        var error = Assert.Throws<InvalidOperationException>(
            () => ModelClients.Create(provider, "any-model", new ConfigurationBuilder().Build(), offline: true));

        Assert.Contains(OfflineMessage, error.Message);
        Assert.DoesNotContain("Missing user secret", error.Message);
    }

    [Fact]
    public void Create_still_builds_the_replay_client_when_offline()
    {
        var fixture = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "replay.jsonl");

        using var client = ModelClients.Create("replay", "any-model", new ConfigurationBuilder().Build(), fixture, offline: true);

        Assert.NotNull(client);
    }

    // The same stop, through the whole app as a process, so the variable is read from the
    // environment the way a test harness or a web host would set it.
    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    public async Task The_app_refuses_a_paid_provider_when_NPCFORGE_OFFLINE_is_set(string provider)
    {
        var run = await ConsoleProcess.RunAsync(["--provider", provider], offline: true);

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains(OfflineMessage, run.Stderr);
    }

    [Fact]
    public async Task The_app_still_replays_when_NPCFORGE_OFFLINE_is_set()
    {
        var fixture = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "replay.jsonl");
        var flags = JsonSerializer.Deserialize<string[]>(File.ReadLines(fixture).First())!;
        var saveFile = Path.Combine(Path.GetTempPath(), $"npcforge-offline-{Guid.NewGuid():N}.json");

        try
        {
            var run = await ConsoleProcess.RunAsync(
                [.. flags, "--provider", "replay", "--recording", fixture],
                offline: true,
                environment: new() { ["NPCFORGE_CHARACTERS"] = saveFile });

            Assert.True(run.ExitCode == 0, run.Stderr);
            Assert.NotNull(JsonNode.Parse(File.ReadAllText(saveFile))!["1"]);
        }
        finally
        {
            File.Delete(saveFile);
        }
    }
}
