using System.Text.Json;

namespace NpcForge;

// The name dice of one brief: the letter the first name starts with, how many syllables it has, and
// the letter the family name starts with (ADR-009). Read from the brief's JSON, which is structured
// data, so the console keeps no copy of the server's enums.
public sealed record NameDice(char Letter, int Syllables, char FamilyLetter)
{
    public static NameDice From(string briefJson)
    {
        using var brief = JsonDocument.Parse(briefJson);
        var root = brief.RootElement;
        return new NameDice(
            LetterOf(root, "nameLetter"),
            // The server writes the count as "1", "2" or "3", so it is read as a number here.
            int.Parse(TextOf(root, "nameSyllables")),
            LetterOf(root, "familyLetter"));
    }

    private static char LetterOf(JsonElement brief, string field) =>
        TextOf(brief, field) is [var letter] ? letter
            : throw new InvalidOperationException($"The brief's {field} is not one letter, so no name can be checked against it.");

    private static string TextOf(JsonElement brief, string field) =>
        brief.TryGetProperty(field, out var value) && value.GetString() is { Length: > 0 } text
            ? text
            : throw new InvalidOperationException($"The brief has no {field}, so no name can be checked against it.");

    // How the dice are shown in a trace: (K, 2, V).
    public override string ToString() => $"({Letter}, {Syllables}, {FamilyLetter})";
}

// A name the check accepted. Syllables are kept as the namer split them, because the count is checked
// on that split, and the save keeps them the same way.
public sealed record NamedCharacter(IReadOnlyList<string> Syllables, string Family)
{
    public string Given => string.Concat(Syllables);
    public string Full => $"{Given} {Family}";
}

public enum NameRejection
{
    None, NoSyllables, EmptySyllable, SyllableWithoutLetters, SyllableWithSpace, WrongLetter, WrongSyllableCount,
    NoFamily, FamilyWithoutLetters, FamilyWithSpace, WrongFamilyLetter, AlreadyNamed,
}

// Checks a name handed back through give_name against the dice. Code cannot hear a name, so it checks
// only what the arguments declare: the letters, and the count as the namer split it. Each rejection
// has its own reason, so the namer, and a test, can tell which rule a name broke (lesson 004).
public static class NameCheck
{
    public static (NameRejection Rejection, string Reason) Check(NameDice dice, IReadOnlyList<string>? syllables, string? family)
    {
        if (syllables is null || syllables.Count == 0)
            return (NameRejection.NoSyllables,
                "Refused: no syllables were given. Hand back the first name split into its syllables, such as [\"Kes\", \"sa\"].");

        var blank = syllables.ToList().FindIndex(string.IsNullOrWhiteSpace);
        if (blank >= 0)
            return (NameRejection.EmptySyllable,
                $"Refused: syllable {blank + 1} of {syllables.Count} is empty. Every syllable needs letters.");

        // A syllable is a piece of one word: a "-", a "1" or a zero-width space is not one, and a space
        // inside it would make Full ambiguous about where the first name ends.
        var noLetters = syllables.ToList().FindIndex(s => !s.Any(char.IsLetter));
        if (noLetters >= 0)
            return (NameRejection.SyllableWithoutLetters,
                $"Refused: syllable {noLetters + 1} of {syllables.Count} (\"{syllables[noLetters]}\") has no letter in it. Every syllable needs letters.");

        var spaced = syllables.ToList().FindIndex(s => s.Trim().Any(char.IsWhiteSpace));
        if (spaced >= 0)
            return (NameRejection.SyllableWithSpace,
                $"Refused: syllable {spaced + 1} of {syllables.Count} (\"{syllables[spaced]}\") has a space inside it. A syllable is part of one word.");

        var given = string.Concat(syllables.Select(s => s.Trim()));
        if (!StartsWith(given, dice.Letter))
            return (NameRejection.WrongLetter,
                $"Refused: the first name must start with {dice.Letter}, and \"{given}\" starts with {given[0]}.");

        if (syllables.Count != dice.Syllables)
            return (NameRejection.WrongSyllableCount,
                $"Refused: the first name must have {dice.Syllables} syllable{(dice.Syllables == 1 ? "" : "s")}, " +
                $"and \"{given}\" was handed back as {syllables.Count}: {JsonSerializer.Serialize(syllables)}.");

        if (string.IsNullOrWhiteSpace(family))
            return (NameRejection.NoFamily,
                $"Refused: no family name was given. Every character has one, and it must start with {dice.FamilyLetter}.");

        var surname = family.Trim();
        if (!surname.Any(char.IsLetter))
            return (NameRejection.FamilyWithoutLetters,
                $"Refused: the family name \"{surname}\" has no letter in it. It must be a word starting with {dice.FamilyLetter}.");

        if (surname.Any(char.IsWhiteSpace))
            return (NameRejection.FamilyWithSpace,
                $"Refused: the family name \"{surname}\" has a space inside it. Give it as one word starting with {dice.FamilyLetter}.");

        if (!StartsWith(surname, dice.FamilyLetter))
            return (NameRejection.WrongFamilyLetter,
                $"Refused: the family name must start with {dice.FamilyLetter}, and \"{surname}\" starts with {surname[0]}.");

        return (NameRejection.None, $"Accepted: {given} {surname}.");
    }

    // Compared as ordinal text, so a dotless i does not stand in for I and a long s does not stand
    // in for S, as a char-by-char upper-casing would let them.
    private static bool StartsWith(string name, char letter) =>
        string.Equals(name[..1], letter.ToString(), StringComparison.OrdinalIgnoreCase);

    // A second name after one was accepted. The first accepted name wins, so the namer cannot swap it
    // for one it likes better.
    public static (NameRejection Rejection, string Reason) AlreadyNamed(NamedCharacter accepted) =>
        (NameRejection.AlreadyNamed,
            $"Refused: this character is already named {accepted.Full}, and the first accepted name stands. Do not call give_name again.");
}
