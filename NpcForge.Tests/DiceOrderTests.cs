using System.Text.Json;
using System.Text.Json.Nodes;

namespace NpcForge.Tests;

// ADR-010 moves when the wants and obstacle dice are read, not when they are cast. These tests hold
// every seed's brief where it was: a golden of seeds 1 to 12 at each difficulty, captured on the code
// before the change, and the real server rolled against it.
public class DiceOrderTests
{
    private static readonly string Golden = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "rolls-seeds-1-to-12.json");

    private static readonly string[] Difficulties = ["Easy", "SomeWork", "Wall"];

    [Fact]
    public async Task Seeds_1_to_12_roll_the_golden_briefs_at_every_difficulty()
    {
        var cases = Enumerable.Range(1, 12).SelectMany(seed => Difficulties.Select(difficulty => (seed, difficulty)));

        // A server of its own for each roll, so no roll is another's dice carrying on. A few at a time:
        // one after another takes minutes, and all 36 at once can outlast the client's start-up wait.
        using var gate = new SemaphoreSlim(6);
        var rolled = await Task.WhenAll(cases.Select(async c =>
        {
            await gate.WaitAsync();
            try { return (key: $"{c.seed}/{c.difficulty}", brief: await RollWithSeed(c.seed, c.difficulty)); }
            finally { gate.Release(); }
        }));
        var now = new JsonObject();
        foreach (var (key, brief) in rolled)
            now[key] = JsonNode.Parse(brief);

        // Missing only when the golden is first captured, on the code before the change.
        if (!File.Exists(Golden))
        {
            File.WriteAllText(Golden, now.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Assert.Fail($"Captured {Golden}. Commit it, then run again.");
        }

        var golden = JsonNode.Parse(File.ReadAllText(Golden))!.AsObject();
        Assert.Equal(golden.Select(e => e.Key).Order(), now.Select(e => e.Key).Order());
        foreach (var (key, was) in golden)
            foreach (var (field, value) in was!.AsObject())
                Assert.True(JsonNode.DeepEquals(value, now[key]![field]), $"{field} moved in {key}: was {value?.ToJsonString()}, now {now[key]![field]?.ToJsonString()}");
    }

    private static async Task<string> RollWithSeed(int seed, string difficulty)
    {
        await using var tools = new McpToolSource(seed: seed);
        await tools.ConnectAsync(CancellationToken.None);
        return await tools.CallDirectAsync("roll_character", new()
        {
            ["setting"] = "an inn in a major city",
            ["playersWant"] = "the name of a fence",
            ["difficulty"] = difficulty,
            ["occupation"] = "innkeeper",
        }, CancellationToken.None);
    }
}
