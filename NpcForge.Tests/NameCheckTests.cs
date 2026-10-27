namespace NpcForge.Tests;

// ADR-009 decision 4: a name handed back through give_name follows its dice, checked on the
// arguments alone. Each rule is broken on its own, by a name that passes every other rule, so a
// rejection can only come from the rule it names (lesson 004).
public class NameCheckTests
{
    private static readonly NameDice KTwoV = new('K', 2, 'V');

    [Fact]
    public void A_name_that_follows_every_die_is_accepted()
    {
        var (rejection, reason) = NameCheck.Check(KTwoV, ["Kes", "sa"], "Vale");

        Assert.Equal(NameRejection.None, rejection);
        Assert.Equal("Accepted: Kessa Vale.", reason);
    }

    [Fact]
    public void Letters_are_matched_ignoring_case()
    {
        Assert.Equal(NameRejection.None, NameCheck.Check(KTwoV, ["kes", "sa"], "vale").Item1);
    }

    public static TheoryData<string[], string, NameRejection, string> Broken => new()
    {
        { [], "Vale", NameRejection.NoSyllables, "no syllables were given" },
        { ["Kes", " "], "Vale", NameRejection.EmptySyllable, "syllable 2 of 2 is empty" },
        { ["To", "bin"], "Vale", NameRejection.WrongLetter, "must start with K, and \"Tobin\" starts with T" },
        { ["Kess"], "Vale", NameRejection.WrongSyllableCount, "must have 2 syllables, and \"Kess\" was handed back as 1" },
        { ["Kes", "sa"], "", NameRejection.NoFamily, "no family name was given" },
        { ["Kes", "sa"], "Mott", NameRejection.WrongFamilyLetter, "family name must start with V, and \"Mott\" starts with M" },
        { ["Kes", "-"], "Vale", NameRejection.SyllableWithoutLetters, "syllable 2 of 2 (\"-\") has no letter" },
        { ["Kes", "1"], "Vale", NameRejection.SyllableWithoutLetters, "syllable 2 of 2 (\"1\") has no letter" },
        { ["Kes", "\u200B"], "Vale", NameRejection.SyllableWithoutLetters, "has no letter in it" },
        { ["Kes", "s a"], "Vale", NameRejection.SyllableWithSpace, "syllable 2 of 2 (\"s a\") has a space inside it" },
        { ["Kes", "sa"], "-", NameRejection.FamilyWithoutLetters, "family name \"-\" has no letter" },
        { ["Kes", "sa"], "\u200B", NameRejection.FamilyWithoutLetters, "has no letter in it" },
        { ["Kes", "sa"], "Van Dyke", NameRejection.FamilyWithSpace, "family name \"Van Dyke\" has a space inside it" },
    };

    [Theory]
    [MemberData(nameof(Broken))]
    public void Each_broken_rule_is_refused_with_its_own_reason(string[] syllables, string family, NameRejection expected, string reason)
    {
        var (rejection, text) = NameCheck.Check(KTwoV, syllables, family);

        Assert.Equal(expected, rejection);
        Assert.Contains(reason, text);
        Assert.StartsWith("Refused: ", text);
    }

    // Every rule has its own reason: no two rejections read the same to the namer. One row per rule,
    // and every rule but None has a row, so a new NameRejection with no row here fails this test.
    [Fact]
    public void No_two_rejections_share_a_reason()
    {
        var rows = Broken.Select(row => (Check: NameCheck.Check(KTwoV, (string[])row[0], (string)row[1]), Expected: (NameRejection)row[2]))
            .GroupBy(r => r.Check.Rejection).Select(g => g.First()).ToList();
        var reasons = rows.Select(r => r.Check.Reason).ToList();
        reasons.Add(NameCheck.AlreadyNamed(new NamedCharacter(["Kes", "sa"], "Vale")).Reason);

        var covered = rows.Select(r => r.Check.Rejection).Append(NameRejection.AlreadyNamed).Order();
        Assert.Equal(Enum.GetValues<NameRejection>().Where(r => r != NameRejection.None).Order(), covered);
        Assert.Equal(reasons.Count, reasons.Distinct().Count());
    }

    // Dotless i upper-cases to I in some cultures and long s to S, so a check that upper-cased the
    // first character would accept them. Neither is the letter the dice named.
    [Fact]
    public void A_dotless_i_is_not_I_and_a_long_s_is_not_S()
    {
        Assert.Equal(NameRejection.WrongLetter, NameCheck.Check(new NameDice('I', 2, 'V'), ["\u0131n", "ga"], "Vale").Item1);
        Assert.Equal(NameRejection.WrongLetter, NameCheck.Check(new NameDice('S', 2, 'V'), ["\u017Fil", "ka"], "Vale").Item1);
        Assert.Equal(NameRejection.WrongFamilyLetter, NameCheck.Check(new NameDice('K', 2, 'I'), ["Kes", "sa"], "\u0131ron").Item1);
        Assert.Equal(NameRejection.WrongFamilyLetter, NameCheck.Check(new NameDice('K', 2, 'S'), ["Kes", "sa"], "\u017Fmith").Item1);
        // And the ordinary letters still match.
        Assert.Equal(NameRejection.None, NameCheck.Check(new NameDice('I', 2, 'S'), ["in", "ga"], "Smith").Item1);
    }

    [Fact]
    public void A_null_family_is_no_family()
    {
        Assert.Equal(NameRejection.NoFamily, NameCheck.Check(KTwoV, ["Kes", "sa"], null).Item1);
    }

    [Fact]
    public void Already_named_names_the_name_that_stands()
    {
        var (rejection, reason) = NameCheck.AlreadyNamed(new NamedCharacter(["Kes", "sa"], "Vale"));

        Assert.Equal(NameRejection.AlreadyNamed, rejection);
        Assert.Contains("already named Kessa Vale", reason);
    }

    [Fact]
    public void The_dice_are_read_from_the_brief_json()
    {
        var dice = NameDice.From("""{"race":"dwarf","nameLetter":"K","nameSyllables":"2","familyLetter":"V"}""");

        Assert.Equal(KTwoV, dice);
        Assert.Equal("(K, 2, V)", dice.ToString());
    }

    [Fact]
    public void A_brief_without_name_dice_names_the_missing_field()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => NameDice.From("""{"nameLetter":"K","familyLetter":"V"}"""));

        Assert.Contains("nameSyllables", error.Message);
    }

    [Fact]
    public void The_given_name_joins_the_syllables_and_the_full_name_adds_the_family()
    {
        var name = new NamedCharacter(["Kes", "sa"], "Vale");

        Assert.Equal("Kessa", name.Given);
        Assert.Equal("Kessa Vale", name.Full);
    }
}
