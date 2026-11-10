using System.Text.Json;
using static NpcForge.Tests.RollArgumentsTests;    // RollArgs: the four answers every roll here starts from


namespace NpcForge.Tests;

// The dice, in process with no model: a handed-in value replaces its die without moving the others
// (ADR-009). That one seed rolls one brief is DiceOrderTests' golden. Every roll here is a seeded Dice
// of its own, unless a test rolls twice on one to see the dice carry on. These rolled on a seeded
// server each until the console called the core in-process (ADR-014).
public class SeededRollTests
{
    [Fact]
    public void A_handed_in_trait_replaces_its_die_without_moving_the_others()
    {
        // ADR-009: every die is cast, so one seed rolls the same brief whatever is handed in.
        // Seed 5 is one where, on the old short-circuit, the dice after attitude visibly moved.
        var rolled = RollWithSeed(5, RollArgs());
        var handed = RollArgs();
        handed["attitude"] = "handed in by the test";
        var withAttitude = RollWithSeed(5, handed);

        var expected = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rolled)!;
        var actual = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(withAttitude)!;

        Assert.Equal("handed in by the test", actual["attitude"].GetString());
        foreach (var (name, value) in expected.Where(e => e.Key != "attitude"))
            Assert.True(value.ToString() == actual[name].ToString(), $"{name} moved when attitude was handed in");
    }

    // ADR-009: every brief carries the five new fields, with no flag and no default: the dice give them.
    [Fact]
    public void Every_brief_carries_the_five_new_fields_rolled_from_their_tables()
    {
        var brief = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(RollWithSeed(5))!;

        Assert.Contains(brief["tone"].GetString(), new[] { "Serious", "Comic", "Heartfelt" });
        Assert.Contains(brief["race"].GetString(), Tables.Races.Select(r => r.Value));
        Assert.Contains(brief["nameLetter"].GetString(), Tables.NameLetters.Select(l => l.Value.ToString()));
        Assert.Contains(brief["nameSyllables"].GetString(), new[] { "1", "2", "3" });     // a string of a digit, as the brief writes it
        Assert.Contains(brief["familyLetter"].GetString(), Tables.NameLetters.Select(l => l.Value.ToString()));
    }

    // ADR-011: every brief carries gender, age and look as strings, after familyLetter, with no flag and
    // no default: the dice give them.
    [Fact]
    public void Every_brief_carries_gender_age_and_look_as_strings_after_the_name_dice()
    {
        using var brief = JsonDocument.Parse(RollWithSeed(5));
        var properties = brief.RootElement.EnumerateObject().ToList();

        Assert.Equal(["gender", "age", "look"], properties.TakeLast(3).Select(p => p.Name));
        Assert.Equal("familyLetter", properties[^4].Name);
        Assert.All(properties.TakeLast(3), p => Assert.Equal(JsonValueKind.String, p.Value.ValueKind));
        Assert.Contains(brief.RootElement.GetProperty("gender").GetString(), new[] { "Male", "Female" });
        Assert.Contains(brief.RootElement.GetProperty("age").GetString(), new[] { "Teenager", "Adult", "MiddleAged", "Old" });
        Assert.False(string.IsNullOrWhiteSpace(brief.RootElement.GetProperty("look").GetString()));
    }

    // ADR-011: a handed-in look, gender or age replaces its result and skips no cast. On one Dice, a roll
    // with the value handed in, then a roll with nothing handed in, must give the second roll a fresh
    // Dice gives after a plain first roll. A skipped cast moves every die after it, and no field of the
    // first roll shows that.
    [Fact]
    public void A_handed_in_look_gender_or_age_moves_no_die_on_the_next_roll()
    {
        var plain = SecondRoll(5, RollArgs());
        var afterEach = new Dictionary<string, string>
        {
            ["look"] = SecondRoll(5, RollArgs("look", "a scar across one cheek")),
            ["gender"] = SecondRoll(5, RollArgs("gender", "Female")),
            ["age"] = SecondRoll(5, RollArgs("age", "Old")),
        };

        foreach (var (field, after) in afterEach)
            Assert.True(plain == after, $"the second roll moved when {field} was handed in on the first");
    }

    // Handing in the value the dice rolled gives the same brief, whole. Seed 4 rolls an old character
    // and seed 9 an adult whose look reads a hair colour, so age is handed in both ways round.
    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void Handing_in_the_rolled_gender_age_or_look_gives_the_same_brief(int seed)
    {
        var plain = RollWithSeed(seed);
        using var brief = JsonDocument.Parse(plain);
        var fields = new[] { "gender", "age", "look" };

        var handed = fields.Select(field =>
            RollWithSeed(seed, RollArgs(field, brief.RootElement.GetProperty(field).GetString()!))).ToArray();

        foreach (var (field, again) in fields.Zip(handed))
            Assert.True(plain == again, $"handing in the rolled {field} changed the brief");
    }

    // Handing in another gender or look changes that field and no other. Seed 5 rolls a male with
    // "big nose or ears"; the test holds that, so the claim cannot go stale unseen.
    [Fact]
    public void Handing_in_another_gender_or_look_changes_only_that_field()
    {
        var handing = new Dictionary<string, (string Value, string Brief)>();
        foreach (var (field, value) in new[] { ("gender", "Female"), ("look", "a scar across one cheek") })
            handing[field] = (value, RollWithSeed(5, RollArgs(field, value)));
        var plain = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(RollWithSeed(5))!;
        Assert.Equal("Male", plain["gender"].GetString());

        foreach (var (field, (value, brief)) in handing)
        {
            var changed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(brief)!;

            Assert.Equal(value, changed[field].GetString());
            foreach (var (name, was) in plain.Where(e => e.Key != field))
                Assert.True(was.ToString() == changed[name].ToString(), $"{name} moved when {field} was handed in");
        }
    }

    // Age is read by the look: an old character is never given the hair colours that are usual on them.
    // Seed 9 rolls an adult whose look is "unusual hair colour: golden, almost metallic". Handed Old, the
    // same hair cast is read against six colours and gives another, and nothing else moves. Handed
    // another age that is not old, the look stays. Held here, so the seed cannot go stale unseen.
    [Fact]
    public void Handing_in_another_age_changes_the_age_and_the_hair_colour_the_look_reads()
    {
        var plain = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(RollWithSeed(9))!;
        var asOld = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(RollWithSeed(9, RollArgs("age", "Old")))!;
        var asTeenager = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(RollWithSeed(9, RollArgs("age", "Teenager")))!;

        Assert.Equal("Adult", plain["age"].GetString());
        Assert.Equal("unusual hair colour: golden, almost metallic", plain["look"].GetString());
        Assert.Equal("Old", asOld["age"].GetString());
        Assert.Equal("unusual hair colour: two colours, half light and half dark", asOld["look"].GetString());
        Assert.Equal("Teenager", asTeenager["age"].GetString());
        Assert.Equal(plain["look"].GetString(), asTeenager["look"].GetString());
        foreach (var (name, was) in plain.Where(e => e.Key is not ("age" or "look")))
        {
            Assert.True(was.ToString() == asOld[name].ToString(), $"{name} moved when age was handed in as Old");
            Assert.True(was.ToString() == asTeenager[name].ToString(), $"{name} moved when age was handed in as Teenager");
        }
    }

    // Each new die, handed in, replaces its result and moves no other die (epic 3's Done when: under
    // one seed, a given tone changes the tone and nothing else). Every value differs from what seed 5
    // rolls (Heartfelt, gnome, C, 3, H), so a value the roll ignored fails too. The control is the
    // roll with nothing handed in, where the die is the only thing that picks. Q is outside the
    // letter dice's table, and still allowed when handed in.
    [Theory]
    [InlineData("tone", "Comic")]
    [InlineData("race", "handed-in race")]
    [InlineData("nameLetter", "Q")]
    [InlineData("nameSyllables", "1")]
    [InlineData("familyLetter", "Q")]
    public void A_handed_in_new_value_replaces_its_die_without_moving_the_others(string parameter, string value)
    {
        var rolled = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(RollWithSeed(5))!;
        var handed = RollArgs();
        handed[parameter] = value;
        var withValue = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(RollWithSeed(5, handed))!;

        Assert.Equal(value, withValue[parameter].GetString());
        foreach (var (name, was) in rolled.Where(e => e.Key != parameter))
            Assert.True(was.ToString() == withValue[name].ToString(), $"{name} moved when {parameter} was handed in");
    }

    // Two rolls on one Dice, the dice carrying on from the first into the second. Returns the second.
    private static string SecondRoll(int seed, Dictionary<string, object?> first)
    {
        var dice = new Dice(seed);
        dice.Roll(first);
        return BriefJson.Of(dice.Roll(RollArgs()));
    }

    private static string RollWithSeed(int seed, Dictionary<string, object?> args) => BriefJson.Of(new Dice(seed).Roll(args));

    // A Dice of its own each time, so the second roll cannot be the first one's dice carrying on.
    private static string RollWithSeed(int seed) => RollWithSeed(seed, RollArgs());
}
