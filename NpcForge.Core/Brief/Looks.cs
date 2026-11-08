using System;
using System.Collections.Generic;

namespace NpcForge
{
    // The five casts the look reads, each a Dice.NextDouble() taken in RollCharacter (ADR-011). A look
    // that is not a 20 reads only the first and the colour casts it needs. The rest are cast anyway,
    // so a look or an age handed in moves no other die.
    public sealed record LookCasts(double Look, double Second, double Third, double Eye, double Hair);

    // One look, and the colour it read when its row names a colour table.
    public sealed record LookPart(Tables.LookRow Row, string? Colour);

    // The look dice, read: the owner's d20, its two more looks on a 20, and the two colour tables.
    // Pure. It takes casts as numbers and casts nothing, so it cannot skip a die (ADR-011).
    public static class Looks
    {
        // The owner's clashes (2026-11-02), each written once and read both ways by Clash, so two
        // looks that clash never come up together in either order. Rows are named by reference, so a
        // reworded row cannot silently drop one. Held here, not on Tables: BriefFieldsTests holds every
        // public array there to six rows, and a clash is not a table the dice read.
        private static readonly (Tables.LookRow A, Tables.LookRow B)[] Clashes =
        [
            (Tables.Short, Tables.Tall),
            (Tables.Bald, Tables.UnusualHairColour),
            (Tables.Bald, Tables.UnusualHairStyle),
            (Tables.ScarOrTattoo, Tables.MultipleScarsOrTattoos),
        ];

        // One part, or two on a 20. Each extra look is read against the rows left once the 20, the first
        // look and its clashes are taken out, so it has the chance a reroll would. The casts do not
        // depend on what comes up, so no die added after them moves.
        public static IReadOnlyList<LookPart> Read(LookCasts casts, Age age)
        {
            var first = Dice.ReadCast(casts.Look, Tables.Looks);
            if (first != Tables.TwoMoreLooks)
                return [Part(first, casts, age)];

            var rows = Tables.Looks.Where(row => row != Tables.TwoMoreLooks).ToArray();
            var second = Dice.ReadCast(casts.Second, rows);
            var third = Dice.ReadCast(casts.Third, rows.Where(row => row != second && !Clash(row, second)).ToArray());
            return [Part(second, casts, age), Part(third, casts, age)];
        }

        // What `look` holds: each part's words, a colour after a colon, and a 20's two parts joined with
        // "; ". No row or colour contains either, so the writer can tell the parts apart.
        public static string Text(IReadOnlyList<LookPart> parts) =>
            string.Join("; ", parts.Select(part => part.Colour is null ? part.Row.Text : $"{part.Row.Text}: {part.Colour}"));

        private static LookPart Part(Tables.LookRow row, LookCasts casts, Age age)
        {
            var colour = row.Colour switch
            {
                Tables.LookColour.Eye => Dice.ReadCast(casts.Eye, Tables.EyeColours),
                Tables.LookColour.Hair => Dice.ReadCast(casts.Hair, HairColoursFor(age)).Text,
                _ => null,
            };
            return new LookPart(row, colour);
        }

        // An old character never gets a colour that is usual on them, which would not be unusual.
        private static Tables.HairColour[] HairColoursFor(Age age) =>
            age == Age.Old ? [.. Tables.HairColours.Where(colour => !colour.UsualWhenOld)] : Tables.HairColours;

        private static bool Clash(Tables.LookRow one, Tables.LookRow other) =>
            Clashes.Any(pair => (pair.A == one && pair.B == other) || (pair.A == other && pair.B == one));
    }
}
