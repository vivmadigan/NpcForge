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
        public static CharacterBrief RollCharacter(
            string setting,
            string playersWant,
            Difficulty difficulty,
            string occupation,
            // Any rolled trait can be handed in instead. Only what is missing gets rolled.
            string? characterWants = null,
            ObstacleKind? obstacle = null,
            string? attitude = null,
            string? mannerism = null,
            string? weakness = null,
            string? willNotDiscuss = null,
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

        // Random.Shared is the dice: the whole of the variety, and the model cannot reach it.
        // An empty table throws here, and the app's CallDirectAsync turns that into a stopped run.
        private static T Pick<T>(IReadOnlyList<T> table) => table[Random.Shared.Next(table.Count)];

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