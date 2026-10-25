using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace NpcForge.Server
{
    [McpServerToolType]
    public static class CharacterTools
    {
        [McpServerTool(Name = "lookup_archetype"), Description("Look up how a given kind of person usually behaves.")]
        public static string LookupArchetype(
            [Description("An occupation, such as innkeeper or farmer")] string occupation)
        {
            return $"A typical {occupation} is busy, watchful, and knows everyone's business.";
        }

        // App-only. The console calls this directly, before the model's first turn, and leaves it
        // out of the model's list: a tool the model can see is a tool it can skip.
        [McpServerTool(Name = "roll_character"), Description("Roll up a character brief inside what the difficulty allows.")]
        // Each parameter's [Description] reaches tools/list. The console builds the scene interview's
        // field list from that schema (ADR-007), so these lines are what the interviewer model reads
        // about each field. The model never sees this tool itself.
        public static CharacterBrief RollCharacter(
            [Description("Where the players meet this character, such as an inn in a major city")]
            string setting,
            [Description("What the players want from this character, such as the name of a fence")]
            string playersWant,
            [Description("How hard it is to get what the players want: Easy, SomeWork or Wall")]
            Difficulty difficulty,
            [Description("Who the character is, such as innkeeper or farmer")]
            string occupation,
            // Any rolled trait can be handed in instead. Only what is missing gets rolled.
            [Description("What the character wants for themselves, such as to be left alone")]
            string? characterWants = null,
            [Description("Why the character does not simply give the players what they want: Wont (will not), Cant (cannot) or ForAPrice (will, at a price)")]
            ObstacleKind? obstacle = null,
            [Description("How the character comes across, such as distrustful or world-weary")]
            string? attitude = null,
            [Description("A habit the players notice, such as drums their fingers on the bar")]
            string? mannerism = null,
            [Description("A weakness the players could use, such as vain or short of money")]
            string? weakness = null,
            [Description("A subject the character will not talk about, such as their family")]
            string? willNotDiscuss = null,
            [Description("Something the character believes that is not true")]
            string? wrongAbout = null)
        {
            // The difficulty filter, in code. Left to the model, it would pick something agreeable.
            var wants = Tables.Wants.Where(w => w.Fits.Contains(difficulty)).ToArray();

            // Every obstacle except Wont when the difficulty is Easy: "they will not" does not fit
            // an ask that almost any attempt clears.
            ObstacleKind[] obstacles = Enum.GetValues<ObstacleKind>().Where(o => !(difficulty == Difficulty.Easy && o == ObstacleKind.Wont)).ToArray();

            return new CharacterBrief
            {
                Setting = setting,
                PlayersWant = playersWant,
                Difficulty = difficulty,
                Occupation = occupation,
                CharacterWants = characterWants ?? Pick(wants).Text,
                Obstacle = obstacle ?? Pick(obstacles),
                Attitude = attitude ?? Pick(Tables.Attitudes),
                Mannerism = mannerism ?? Pick(Tables.Mannerisms),
                Weakness = weakness ?? Pick(Tables.Weaknesses),
                WillNotDiscuss = willNotDiscuss ?? Pick(Tables.WillNotDiscuss),
                WrongAbout = wrongAbout ?? Pick(Tables.WrongAbout),
            };
        }

        // The dice: the whole of the variety, and the model cannot reach them. An empty table
        // throws here, and the app's CallDirectAsync turns that into a stopped run.
        private static T Pick<T>(IReadOnlyList<T> table) => table[Dice.Next(table.Count)];

        // With NPCFORGE_SEED set, the same seed rolls the same brief every time, because
        // RollCharacter always picks in the same order. Without it, Random.Shared, as before.
        // Made once per server process, and the console starts a new server for every run.
        private static readonly Random Dice = CreateDice();

        private static Random CreateDice()
        {
            var seed = Environment.GetEnvironmentVariable("NPCFORGE_SEED");

            // stderr, never stdout: stdout is the wire. The console shows it as [server] dice ...
            Console.Error.WriteLine(seed is null ? "dice: random" : $"dice: seed {seed}");

            return seed is null ? Random.Shared : new Random(int.Parse(seed));
        }

        // App-only. The app saves every run once the model has written. Returns the new number.
        // The brief must arrive as a JSON object, not as the text the roll returned: sent as
        // text, it fails with no reason given.
        [McpServerTool(Name = "save_character"), Description("Keep a written character and the brief it was written from.")]
        public static int SaveCharacter(CharacterBrief brief, string text)
        {
            var all = Storage.Load();

            // One past the highest, not Count + 1: after a hand edit that deletes an entry,
            // Count + 1 could save over the last one.
            var number = all.Count == 0 ? 1 : all.Keys.Max() + 1;

            all[number] = new SavedCharacter(brief, text);
            Storage.Save(all);
            return number;

        }

        // App-only. --load calls this instead of rolling and instead of the model. An unknown
        // number throws McpException, so the run stops with a reason, not with an empty brief.
        [McpServerTool(Name = "load_character"), Description("Load a kept character by its number.")]
        public static SavedCharacter LoadCharacter(int number)
        {
            var all = Storage.Load();

            if (!all.TryGetValue(number, out var saved))
                throw new McpException($"No character saved as {number}.");

            return saved;

        }

    }
}