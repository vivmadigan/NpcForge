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

            // Every ObstacleKind except Wont when the difficulty is Easy: an easy character can
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
    }
}
