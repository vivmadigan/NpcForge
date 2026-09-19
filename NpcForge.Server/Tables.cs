using System;
using System.Collections.Generic;
using System.Text;

namespace NpcForge.Server
{
    public static class Tables
    {
        public record Want(string Text, params Difficulty[] Fits);

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
    }
}
