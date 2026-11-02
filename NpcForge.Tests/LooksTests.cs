using NpcForge.Server;

namespace NpcForge.Tests;

// ADR-011: the look dice, read in process with no server. Looks.Read is pure, casts in and parts out,
// so the tests walk a grid of casts instead of rolling: every row, every pair and every colour is
// read, and the counts are exact. The clashes and the old-hair rule are stated here from the owner's
// words (ROADMAP epic 10), not read back from the code under test.
public class LooksTests
{
    // The owner's d20 as the owner numbers it, so a test reads the table the owner wrote.
    private static Tables.LookRow Row(int owner) => Tables.Looks[owner - 1];

    // The owner's clashes by those numbers: short (7) with tall (8), bald (2) with a hair look, which
    // is an unusual hair colour (14) or hair or beard style (15), and a scar or tattoo (16) with multiple (17).
    private static readonly (int A, int B)[] OwnersClashes = [(7, 8), (2, 14), (2, 15), (16, 17)];

    private static bool Clashing(Tables.LookRow one, Tables.LookRow other) =>
        OwnersClashes.Any(clash => (Row(clash.A) == one && Row(clash.B) == other) || (Row(clash.A) == other && Row(clash.B) == one));

    // The middle of each of `count` equal slices of [0, 1), so every row a cast can name is named the
    // same number of times, and no cast sits on a boundary.
    private static IEnumerable<double> Grid(int count) => Enumerable.Range(0, count).Select(i => (i + 0.5) / count);

    // A d20 cast that reads the owner's row, with the rest of the casts at the middle.
    private static LookCasts Casting(int owner, double eye = 0.5, double hair = 0.5) => new((owner - 0.5) / 20, 0.5, 0.5, eye, hair);

    private static IReadOnlyList<LookPart> Read(LookCasts casts, Age age = Age.Adult) => Looks.Read(casts, age);

    // Every read of a 20: the d20 on 20, and the two more looks and the hair cast walked.
    private static IEnumerable<IReadOnlyList<LookPart>> EveryTwenty(Age age, int slices, int hairSlices = 1)
    {
        foreach (var second in Grid(slices))
            foreach (var third in Grid(slices))
                foreach (var hair in Grid(hairSlices))
                    yield return Read(new LookCasts(19.5 / 20, second, third, 0.5, hair), age);
    }

    // The owner's d20 has twenty rows and 20 is "two more looks". A d20 read against the nineteen rows
    // that make a look would never come up 20 and would give every other row one chance in nineteen.
    [Fact]
    public void Each_of_the_nineteen_looks_comes_up_one_time_in_twenty_and_so_does_a_20()
    {
        var reads = Grid(2000).Select(cast => Read(new LookCasts(cast, 0.5, 0.5, 0.5, 0.5))).ToList();

        foreach (var owner in Enumerable.Range(1, 19))
            Assert.True(reads.Count(parts => parts is [var only] && only.Row == Row(owner)) == 100, $"row {owner} ({Row(owner).Text}) did not come up 100 times in 2000");
        Assert.Equal(100, reads.Count(parts => parts.Count == 2));
        Assert.Equal(2000, reads.Count(parts => parts.Count is 1 or 2));
    }

    // Done when: a 20 gives two looks that never repeat, never are 20 and never clash. In either
    // order: the grid names every row first and every row second.
    [Fact]
    public void A_20_gives_two_looks_that_never_repeat_are_never_20_and_never_clash_in_either_order()
    {
        var pairs = new HashSet<(Tables.LookRow First, Tables.LookRow Second)>();

        foreach (var parts in EveryTwenty(Age.Adult, slices: 100))
        {
            Assert.Equal(2, parts.Count);
            var (first, second) = (parts[0].Row, parts[1].Row);
            Assert.True(first != second, $"{first.Text} came up twice");
            Assert.True(first != Tables.TwoMoreLooks && second != Tables.TwoMoreLooks, $"a 20 gave 20: {first.Text}; {second.Text}");
            Assert.False(Clashing(first, second), $"{first.Text} clashes with {second.Text}");
            pairs.Add((first, second));
        }

        // Every ordered pair the owner's rules allow came up, so no pair went unchecked and none was
        // lost: nineteen looks, each with the eighteen others, less each clash in both orders.
        Assert.Equal(19 * 18 - 2 * OwnersClashes.Length, pairs.Count);
    }

    // Done when: an old character never gets snow white or silver hair. The same casts give both to an
    // adult, so the test can fail: it would pass for an old character if those rows were never read.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_old_character_never_gets_snow_white_or_silver_hair_and_an_adult_does(bool onATwenty)
    {
        List<string> HairColours(Age age) => onATwenty
            ? [.. EveryTwenty(age, slices: 40, hairSlices: 20).SelectMany(parts => parts).Where(part => part.Row == Row(14)).Select(part => part.Colour!)]
            : [.. Grid(80).Select(hair => Read(Casting(14, hair: hair), age).Single().Colour!)];

        var old = HairColours(Age.Old).Distinct().Order().ToList();
        var adult = HairColours(Age.Adult).Distinct().Order().ToList();

        Assert.DoesNotContain("snow white", old);
        Assert.DoesNotContain("silver", old);
        Assert.Equal(6, old.Count);                                                           // the other six, all of them
        Assert.Contains("snow white", adult);
        Assert.Contains("silver", adult);
        Assert.Equal(8, adult.Count);
        // Only the hair is read against fewer rows: an old character still gets all eight eye colours.
        Assert.Equal(8, Grid(80).Select(eye => Read(Casting(13, eye: eye), Age.Old).Single().Colour).Distinct().Count());
    }

    // The old-hair rule reads the same cast against the six rows left, so the top of the die is silver
    // for anyone else and the last of the six for an old character.
    [Fact]
    public void An_old_characters_hair_is_read_against_the_six_colours_left()
    {
        Assert.Equal("silver", Read(Casting(14, hair: 0.99), Age.MiddleAged).Single().Colour);
        Assert.Equal("golden, almost metallic", Read(Casting(14, hair: 0.99), Age.Old).Single().Colour);
        Assert.Equal("snow white", Read(Casting(14, hair: 0.2), Age.Teenager).Single().Colour);
        Assert.Equal("copper red", Read(Casting(14, hair: 0.0), Age.Old).Single().Colour);
        Assert.Equal("blue-black", Read(Casting(14, hair: 0.2), Age.Old).Single().Colour);
    }

    // The colour casts are read only by a row that names a colour table, and the two tables are read apart.
    [Fact]
    public void A_colour_is_read_by_the_row_that_names_its_table_and_by_no_other()
    {
        Assert.Equal("unusual eye colour", Read(Casting(13, eye: 0.0)).Single().Row.Text);
        Assert.Equal("violet", Read(Casting(13, eye: 0.0, hair: 0.99)).Single().Colour);
        Assert.Equal("red", Read(Casting(13, eye: 0.99, hair: 0.0)).Single().Colour);
        Assert.Equal("copper red", Read(Casting(14, eye: 0.99, hair: 0.0)).Single().Colour);
        // No other row has a colour, whatever the colour casts are.
        foreach (var owner in Enumerable.Range(1, 19).Except([13, 14]))
            Assert.Null(Read(Casting(owner, eye: 0.3, hair: 0.3)).Single().Colour);
    }

    // What `look` holds: each part's words, a colour after a colon, and a 20's two parts joined with "; ".
    [Fact]
    public void The_look_text_puts_a_colour_after_a_colon_and_joins_a_20s_two_parts_with_a_semicolon()
    {
        Assert.Equal("bald", Looks.Text(Read(Casting(2))));
        Assert.Equal("unusual eye colour: violet", Looks.Text(Read(Casting(13, eye: 0.0))));
        Assert.Equal("unusual hair colour: copper red", Looks.Text(Read(Casting(14, hair: 0.0))));

        var twenties = EveryTwenty(Age.Adult, slices: 100).Select(Looks.Text).ToList();
        Assert.Contains("bald; rare item or weapon", twenties);
        Assert.All(twenties, text => Assert.Equal(2, text.Split("; ").Length));
        // A colour part inside a 20 keeps its colon, and the colon is not a join.
        Assert.Contains(twenties, text => text.Contains("unusual eye colour: ") && text.Contains("; "));
    }

    // A reader of `look` splits on "; " and takes ": " as a colour, so no row or colour may hold either.
    [Fact]
    public void No_row_or_colour_holds_the_separators_the_look_text_uses()
    {
        string[] words = [.. Tables.Looks.Select(row => row.Text), .. Tables.EyeColours, .. Tables.HairColours.Select(colour => colour.Text)];

        Assert.All(words, word => Assert.DoesNotContain("; ", word));
        Assert.All(words, word => Assert.DoesNotContain(": ", word));
    }
}
