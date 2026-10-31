using NpcForge.Server;
using System.Reflection;

namespace NpcForge.Tests;

// ADR-009 build order item 6, the server's half, free and in process: the tables behind the new
// dice, the weighted pick, and the brief and roll_character staying one set of fields.
public class BriefFieldsTests
{
    // Every table in Tables.cs, by name. A table added later is found here with no edit.
    private static IEnumerable<(string Name, int Rows)> AllTables() =>
        typeof(Tables).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType.IsArray)
            .Select(field => (field.Name, ((Array)field.GetValue(null)!).Length));

    // Six rows is the least a text table may have, so a trait does not repeat across a few characters.
    // SyllableCounts and Difficulties are exempt by name, as the fixed choices are: tone is an enum with
    // no table, and each has three values. The same exemption `obstacle` has.
    [Fact]
    public void Every_text_table_has_at_least_six_rows()
    {
        var tables = AllTables().ToList();

        Assert.True(tables.Count >= 8, $"Only {tables.Count} tables were found; the reflection is looking in the wrong place.");
        var tooShort = tables.Where(t => t.Name is not (nameof(Tables.SyllableCounts) or nameof(Tables.Difficulties)) && t.Rows < 6).ToList();
        Assert.True(tooShort.Count == 0, "Fewer than six rows: " + string.Join(", ", tooShort.Select(t => $"{t.Name} ({t.Rows})")));
    }

    // The owner's rows, settled 2026-10-25: eleven races, total weight 27.
    [Fact]
    public void The_race_table_holds_the_owners_rows()
    {
        Assert.Equal(
            [("human", 7), ("dwarf", 3), ("elf", 3), ("halfling", 3), ("gnome", 2), ("half-orc", 2),
             ("orc", 2), ("goblin", 2), ("dragonborn", 1), ("goliath", 1), ("tiefling", 1)],
            Tables.Races.Select(r => (r.Value, r.Weight)));
        Assert.Equal(27, Tables.Races.Sum(r => r.Weight));
    }

    [Fact]
    public void The_letter_table_is_every_letter_but_Q_U_X_Y_Z_at_equal_weight()
    {
        var letters = Tables.NameLetters.Select(l => l.Value.ToString()).ToList();

        Assert.Equal(21, letters.Count);
        Assert.Equal(letters.Distinct(), letters);
        Assert.Empty(letters.Intersect(["Q", "U", "X", "Y", "Z"]));
        Assert.All(Tables.NameLetters, l => Assert.Equal(1, l.Weight));
    }

    [Fact]
    public void The_syllable_table_is_one_two_two_one_and_no_four()
    {
        Assert.Equal([(Syllables.One, 1), (Syllables.Two, 2), (Syllables.Three, 1)],
            Tables.SyllableCounts.Select(s => (s.Value, s.Weight)));
    }

    // The owner's rows, settled 2026-10-30 (ADR-010): a missing difficulty is most often some work.
    [Fact]
    public void The_difficulty_table_is_easy_one_some_work_two_wall_one()
    {
        Assert.Equal([(Difficulty.Easy, 1), (Difficulty.SomeWork, 2), (Difficulty.Wall, 1)],
            Tables.Difficulties.Select(d => (d.Value, d.Weight)));
    }

    // Many seeded picks land near their weights: a pick that ignored them (uniform over the rows) would
    // give every race about 2,455 of 27,000, and human would miss its 7,000 by thousands.
    [Fact]
    public void The_weighted_pick_follows_its_weights_over_many_seeded_rolls()
    {
        const int rolls = 27_000;                                // 1,000 per unit of weight
        var dice = new Random(7);
        var counts = Enumerable.Range(0, rolls)
            .Select(_ => Tables.PickWeighted(Tables.Races, dice))
            .GroupBy(race => race)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var race in Tables.Races)
        {
            var expected = race.Weight * 1_000;
            Assert.InRange(counts[race.Value], expected * 0.85, expected * 1.15);
        }
    }

    [Fact]
    public void The_same_seed_picks_the_same_weighted_rows()
    {
        var one = new Random(3);
        var two = new Random(3);

        Assert.Equal(
            Enumerable.Range(0, 50).Select(_ => Tables.PickWeighted(Tables.Races, one)).ToList(),
            Enumerable.Range(0, 50).Select(_ => Tables.PickWeighted(Tables.Races, two)).ToList());
    }

    [Fact]
    public void A_weighted_table_with_no_rows_cannot_be_picked_from()
    {
        Assert.Throws<InvalidOperationException>(() => Tables.PickWeighted<string>([], new Random(1)));
    }

    // The brief and roll_character's parameters are one list of fields, set by name. A field on one and
    // not the other would roll and not reach the brief, or the reverse.
    [Fact]
    public void The_briefs_properties_are_the_same_set_as_roll_characters_parameters()
    {
        string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

        var properties = typeof(CharacterBrief).GetProperties().Select(p => Camel(p.Name)).Order();
        var parameters = typeof(CharacterTools).GetMethod(nameof(CharacterTools.RollCharacter))!
            .GetParameters().Select(p => p.Name!).Order();

        Assert.Equal(properties, parameters);
    }
}
