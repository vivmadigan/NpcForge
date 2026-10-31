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

        var rolled = await RollSixAtATime(cases, c => RollWithSeed(c.seed, c.difficulty));
        var now = new JsonObject();
        foreach (var (c, brief) in rolled)
            now[$"{c.seed}/{c.difficulty}"] = brief;

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

    // ADR-010: a missing difficulty is rolled, as the thirteenth die. Seeds 1 to 12 with none handed in,
    // checked against the golden, where the same seeds had each difficulty handed in:
    // - all three difficulties come up, so the die reads the whole table;
    // - every die but the wants and obstacle, which read the difficulty, matches every handed-in case;
    // - the case handed the difficulty the die rolled matches whole, so rolled and handed in are one;
    // - a rolled Easy never rolls Wont. Seeds 5, 8 and 11 roll Easy, and each one's obstacle cast is
    //   under a third, so the full three-row table would read it as Wont (worked out from
    //   new Random(seed) when this test was written). The Easy rule must read the rolled difficulty to
    //   pass. The test holds that those three still roll Easy, so the claim cannot go stale unseen.
    [Fact]
    public async Task A_missing_difficulty_is_rolled_last_and_rolls_what_handing_it_in_rolls()
    {
        var golden = JsonNode.Parse(File.ReadAllText(Golden))!.AsObject();
        var rolled = await RollSixAtATime(Enumerable.Range(1, 12), seed => RollWithSeed(seed, difficulty: null));

        Assert.Equal(Difficulties, rolled.Select(r => r.brief["difficulty"]!.GetValue<string>()).Distinct().Order());
        foreach (var seed in new[] { 5, 8, 11 })
            Assert.Equal("Easy", rolled.Single(r => r.key == seed).brief["difficulty"]!.GetValue<string>());
        foreach (var (seed, brief) in rolled)
        {
            var difficulty = brief["difficulty"]!.GetValue<string>();
            Assert.True(JsonNode.DeepEquals(golden[$"{seed}/{difficulty}"], brief), $"seed {seed} rolled {difficulty} but not the brief handed {difficulty} rolls");
            foreach (var handed in Difficulties)
                foreach (var (field, value) in golden[$"{seed}/{handed}"]!.AsObject())
                    if (field is not ("difficulty" or "characterWants" or "obstacle"))
                        Assert.True(JsonNode.DeepEquals(value, brief[field]), $"{field} moved in seed {seed} between rolled and {handed} handed in");
            if (difficulty == "Easy")
                Assert.NotEqual("Wont", brief["obstacle"]!.GetValue<string>());
        }
    }

    // The reorder rests on this: on a seeded Random, a cast read late against a table of n rows picks
    // the row Next(n) would have picked. A run of casts per seed, so a drift between them shows too.
    [Fact]
    public void A_cast_read_late_picks_the_row_Next_would_have_picked()
    {
        for (var seed = 0; seed < 200; seed++)
            for (var rows = 1; rows <= 40; rows++)
            {
                var table = Enumerable.Range(0, rows).ToArray();
                var picking = new Random(seed);
                var casting = new Random(seed);
                for (var cast = 0; cast < 5; cast++)
                    Assert.True(picking.Next(rows) == NpcForge.Server.CharacterTools.ReadCast(casting.NextDouble(), table),
                        $"seed {seed}, {rows} rows, cast {cast}");
            }
    }

    // A server of its own for each roll, so no roll is another's dice carrying on. A few at a time:
    // one after another takes minutes, and all at once can outlast the client's start-up wait.
    private static async Task<(T key, JsonObject brief)[]> RollSixAtATime<T>(IEnumerable<T> keys, Func<T, Task<string>> roll)
    {
        using var gate = new SemaphoreSlim(6);
        return await Task.WhenAll(keys.Select(async key =>
        {
            await gate.WaitAsync();
            try { return (key, JsonNode.Parse(await roll(key))!.AsObject()); }
            finally { gate.Release(); }
        }));
    }

    // A null difficulty is left out of the arguments, as the interview leaves out a rolled field.
    private static async Task<string> RollWithSeed(int seed, string? difficulty)
    {
        await using var tools = new McpToolSource(seed: seed);
        await tools.ConnectAsync(CancellationToken.None);
        var args = new Dictionary<string, object?>
        {
            ["setting"] = "an inn in a major city",
            ["playersWant"] = "the name of a fence",
            ["occupation"] = "innkeeper",
        };
        if (difficulty is not null)
            args["difficulty"] = difficulty;
        return await tools.CallDirectAsync("roll_character", args, CancellationToken.None);
    }
}
