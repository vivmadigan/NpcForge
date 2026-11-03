using System;
using System.Collections.Generic;
using System.Text;

namespace NpcForge.Server
{
    public static class Tables
    {
        public record Want(string Text, params Difficulty[] Fits);

        // ADR-011. A look row says whether it reads a colour table, so no code finds a row by its text.
        public enum LookColour { None, Eye, Hair }
        public sealed record LookRow(string Text, LookColour Colour = LookColour.None);

        // A hair colour says whether it would be usual on an old character, so the rule travels with
        // the row and a reworded colour cannot drop it.
        public sealed record HairColour(string Text, bool UsualWhenOld = false);

        // A row that comes up Weight times in the total. The first weighted tables (ADR-009); the
        // older ones are plain arrays because every row weighs the same.
        public record Weighted<T>(T Value, int Weight);

        // One cast of the dice per pick, so a weighted die costs the seed exactly what a plain one does.
        public static T PickWeighted<T>(IReadOnlyList<Weighted<T>> table, Random dice)
        {
            var roll = dice.Next(table.Sum(row => row.Weight));
            foreach (var row in table)
            {
                if (roll < row.Weight)
                    return row.Value;
                roll -= row.Weight;
            }
            throw new InvalidOperationException("A weighted table with no rows cannot be picked from.");
        }

        public static readonly Want[] Wants =
        [
            new("a bit of company",
                Difficulty.Easy),

            new("to be paid what they are owed",
                Difficulty.SomeWork),

            new("to be left alone",
                Difficulty.SomeWork, Difficulty.Wall),

            new("to keep a secret buried",
                Difficulty.Wall),

            new("to seem more important than they are",
                Difficulty.Easy, Difficulty.SomeWork),

            new("to hear some gossip",
                Difficulty.Easy, Difficulty.SomeWork),

            new("to get some help with a problem",
                Difficulty.SomeWork),

            new("to make a bet for money",
                Difficulty.Easy, Difficulty.SomeWork),

            new("to afford something they have their eye on",
                Difficulty.SomeWork),

            new("to sell something they no longer want",
                Difficulty.Easy, Difficulty.SomeWork),

            new("to win someone's favour",
                Difficulty.Easy, Difficulty.SomeWork),

            new("to settle a petty score",
                Difficulty.SomeWork, Difficulty.Wall),

            new("to get a decent meal",
                Difficulty.Easy),

            new("to avoid someone they owe a favour to",
                Difficulty.SomeWork, Difficulty.Wall),

            new("to find something they have misplaced",
                Difficulty.Easy, Difficulty.SomeWork),

            new("to repay a kindness",
                Difficulty.Easy, Difficulty.SomeWork),

            new("to get out of an unpleasant responsibility",
                Difficulty.SomeWork, Difficulty.Wall),

            new("to find out whether a rumour is true",
                Difficulty.Easy, Difficulty.SomeWork),

            new("to get invited somewhere they usually aren't welcome",
                Difficulty.SomeWork),

            new("to try something they have never done before",
                Difficulty.Easy, Difficulty.SomeWork)
        ];

        public static readonly string[] Attitudes =
        [
            "greedy",
            "rushed off their feet",
            "distrustful",
            "frightened",
            "overly welcoming",
            "proud",
            "superstitious",
            "eager to impress",
            "easily offended",
            "world-weary",
            "cheerfully oblivious",
            "overly familiar",
            "needlessly competitive",
            "trying hard to sound professional",
            "nosy",
            "blunt",
            "self-pitying",
            "condescending",
            "excitable",
            "dutiful"
        ];

        public static readonly string[] Mannerisms =
        [
            "talks with their hands",
            "finds something to fiddle with",
            "stares a little too long",
            "invades personal space",
            "fusses with their appearance",
            "laughs nervously",
            "chooses every word carefully",
            "smiles at the wrong moments",
            "lets silence linger",
            "answers questions with questions",
            "answers before others finish speaking",
            "trails off mid-sentence",
            "mutters under their breath",
            "sighs before answering",
            "nods before people have finished speaking",
            "tilts their head when listening",
            "speaks louder than necessary",
            "lowers their voice as though sharing a secret",
            "checks whether people laughed at their jokes",
            "uses impressive words incorrectly"
        ];

        public static readonly string[] Weaknesses =
        [
            "is easily flattered",
            "finds it hard to turn down a wager",
            "has a soft spot for animals",
            "is easily tempted by food or drink",
            "feels compelled to correct people",
            "is easily impressed by confident people",
            "cannot bear being left out",
            "is easily distracted by gossip",
            "finds it hard to refuse a polite request",
            "feels obliged to repay small kindnesses"
        ];

        public static readonly string[] WillNotDiscuss =
        [
            "their age",
            "how much money they have",
            "a nickname they dislike",
            "a game they recently lost",
            "how they lost a tooth",
            "their terrible sense of direction",
            "someone they have a crush on",
            "their cooking ability",
            "an embarrassing superstition",
            "their plans for later"
        ];

        public static readonly string[] WrongAbout =
        [
            "believes they are an excellent liar",
            "thinks whispering means nobody else can hear them",
            "assumes the best-dressed person is in charge",
            "thinks all adventurers know one another",
            "believes expensive things are always better",
            "misunderstands a common saying and keeps using it",
            "believes they have an excellent singing voice",
            "thinks ignoring a problem usually makes it go away",
            "believes a harmless habit brings terrible luck",
            "thinks a common animal is much more dangerous than it is"
        ];

        // The owner's rows (ADR-009, settled 2026-10-25): about one character in four is human.
        public static readonly Weighted<string>[] Races =
        [
            new("human", 7),
            new("dwarf", 3),
            new("elf", 3),
            new("halfling", 3),
            new("gnome", 2),
            new("half-orc", 2),
            new("orc", 2),
            new("goblin", 2),
            new("dragonborn", 1),
            new("goliath", 1),
            new("tiefling", 1),
        ];

        // Every letter but Q, U, X, Y and Z, at equal weight: few names start with those. Used for the
        // first name's letter and the family name's. A GM can still hand in a Q.
        public static readonly Weighted<NameLetter>[] NameLetters =
        [
            .. Enum.GetValues<NameLetter>()
                .Where(letter => letter is not (NameLetter.Q or NameLetter.U or NameLetter.X or NameLetter.Y or NameLetter.Z))
                .Select(letter => new Weighted<NameLetter>(letter, 1)),
        ];

        // The owner's rows (ADR-010): a missing difficulty is most often some work. Three rows, the
        // whole of the choice, so exempt from the six-row rule by name, as SyllableCounts is.
        public static readonly Weighted<Difficulty>[] Difficulties =
        [
            new(Difficulty.Easy, 1),
            new(Difficulty.SomeWork, 2),
            new(Difficulty.Wall, 1),
        ];

        public static readonly Weighted<Syllables>[] SyllableCounts =
        [
            new(Syllables.One, 1),
            new(Syllables.Two, 2),
            new(Syllables.Three, 1),
        ];

        // The owner's d20 of looks (ADR-011, settled 2026-11-02). The rows Looks.cs puts in a clash, and
        // the 20, are fields of their own, so it names them by reference and never by their text.
        public static readonly LookRow Short = new("short");
        public static readonly LookRow Tall = new("tall");
        public static readonly LookRow Bald = new("bald");
        public static readonly LookRow UnusualHairColour = new("unusual hair colour", LookColour.Hair);
        public static readonly LookRow UnusualHairStyle = new("unusual hair or beard style");
        public static readonly LookRow ScarOrTattoo = new("scar or tattoo");
        public static readonly LookRow MultipleScarsOrTattoos = new("multiple scars or tattoos");
        public static readonly LookRow TwoMoreLooks = new("two more looks");

        // All twenty rows, as the owner's die reads them. Row 20 is "two more looks", so a brief never
        // holds it: Looks.Read turns it into two looks.
        public static readonly LookRow[] Looks =
        [
            new("bad posture"),
            Bald,
            new("beautiful"),
            new("big nose or ears"),
            new("cross-eyed"),
            new("pale"),
            Short,
            Tall,
            new("unique armour"),
            new("unique clothing"),
            new("unique jewellery"),
            new("unique emblem or symbol"),
            new("unusual eye colour", LookColour.Eye),
            UnusualHairColour,
            UnusualHairStyle,
            ScarOrTattoo,
            MultipleScarsOrTattoos,
            new("rare item or weapon"),
            new("paralysed part"),
            TwoMoreLooks,
        ];

        public static readonly string[] EyeColours =
        [
            "violet",
            "amber, almost gold",
            "pale grey, nearly colourless",
            "one blue, one brown",
            "bright green",
            "so dark they look black",
            "silver",
            "red",
        ];

        // Snow white and silver are what an old head of hair usually is, so they are not unusual on an
        // old character and Looks.Read leaves them out for one.
        public static readonly HairColour[] HairColours =
        [
            new("copper red"),
            new("snow white", UsualWhenOld: true),
            new("blue-black"),
            new("a single white streak"),
            new("dyed a bright colour"),
            new("two colours, half light and half dark"),
            new("golden, almost metallic"),
            new("silver", UsualWhenOld: true),
        ];
    }
}
