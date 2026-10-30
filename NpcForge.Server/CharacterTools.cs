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
            // Any rolled trait can be handed in instead. Its die is still cast; the handed-in value wins.
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
            string? wrongAbout = null,
            [Description("How the character is written: Serious, Comic or Heartfelt. It changes the writing, not what is true of them")]
            Tone? tone = null,
            [Description("The character's people, such as dwarf or half-orc")]
            string? race = null,
            [Description("The letter the character's first name starts with")]
            NameLetter? nameLetter = null,
            [Description("How many syllables the character's first name has: 1, 2 or 3")]
            Syllables? nameSyllables = null,
            [Description("The letter the character's family name starts with")]
            NameLetter? familyLetter = null)
        {
            // The SDK's string-enum converter also accepts a number, so an undefined enum would bind
            // and be written back as that number. Refused here, before any die is cast, by name.
            RequireDefined(nameof(difficulty), difficulty);
            RequireDefined(nameof(obstacle), obstacle);
            RequireDefined(nameof(tone), tone);
            RequireDefined(nameof(nameLetter), nameLetter);
            RequireDefined(nameof(nameSyllables), nameSyllables);
            RequireDefined(nameof(familyLetter), familyLetter);

            // Every die is cast, every time, in this order, and a handed-in value replaces its die's
            // result instead of skipping the die. One seed then rolls the same dice whatever is
            // handed in (ADR-009). `x ?? Pick(...)` would skip the die and move every die after it.
            // The wants and obstacle dice are cast first but read last, once the difficulty their
            // tables depend on is settled (ADR-010).
            var wantsCast = Dice.NextDouble();
            var obstacleCast = Dice.NextDouble();
            var rolledAttitude = Pick(Tables.Attitudes);
            var rolledMannerism = Pick(Tables.Mannerisms);
            var rolledWeakness = Pick(Tables.Weaknesses);
            var rolledWillNotDiscuss = Pick(Tables.WillNotDiscuss);
            var rolledWrongAbout = Pick(Tables.WrongAbout);
            var rolledTone = Pick(Enum.GetValues<Tone>());
            var rolledRace = Tables.PickWeighted(Tables.Races, Dice);
            var rolledNameLetter = Tables.PickWeighted(Tables.NameLetters, Dice);
            var rolledNameSyllables = Tables.PickWeighted(Tables.SyllableCounts, Dice);
            var rolledFamilyLetter = Tables.PickWeighted(Tables.NameLetters, Dice);

            // The difficulty filter, in code. Left to the model, it would pick something agreeable.
            var wants = Tables.Wants.Where(w => w.Fits.Contains(difficulty)).ToArray();

            // Every obstacle except Wont when the difficulty is Easy: "they will not" does not fit
            // an ask that almost any attempt clears.
            ObstacleKind[] obstacles = Enum.GetValues<ObstacleKind>().Where(o => !(difficulty == Difficulty.Easy && o == ObstacleKind.Wont)).ToArray();

            var rolledWants = ReadCast(wantsCast, wants).Text;
            var rolledObstacle = ReadCast(obstacleCast, obstacles);

            return new CharacterBrief
            {
                Setting = setting,
                PlayersWant = playersWant,
                Difficulty = difficulty,
                Occupation = occupation,
                CharacterWants = characterWants ?? rolledWants,
                Obstacle = obstacle ?? rolledObstacle,
                Attitude = attitude ?? rolledAttitude,
                Mannerism = mannerism ?? rolledMannerism,
                Weakness = weakness ?? rolledWeakness,
                WillNotDiscuss = willNotDiscuss ?? rolledWillNotDiscuss,
                WrongAbout = wrongAbout ?? rolledWrongAbout,
                Tone = tone ?? rolledTone,
                Race = race ?? rolledRace,
                NameLetter = nameLetter ?? rolledNameLetter,
                NameSyllables = nameSyllables ?? rolledNameSyllables,
                FamilyLetter = familyLetter ?? rolledFamilyLetter,
            };
        }

        private static void RequireDefined<T>(string parameter, T? value) where T : struct, Enum
        {
            if (value is { } given && !Enum.IsDefined(given))
                throw new McpException($"{parameter} {Convert.ToInt32(given)} is not one of its values.");
        }

        private static void RequireDefined<T>(string parameter, T value) where T : struct, Enum
            => RequireDefined(parameter, (T?)value);

        // The dice: the whole of the variety, and the model cannot reach them. An empty table
        // throws here, and the app's CallDirectAsync turns that into a stopped run.
        private static T Pick<T>(IReadOnlyList<T> table) => table[Dice.Next(table.Count)];

        // A die cast now, as Dice.NextDouble(), and read later against its table. Seeded Random
        // computes Next(n) as (int)(NextDouble() * n), so this picks the row Pick would have picked
        // had the table been known at the cast, and every seed keeps its brief (ADR-010). A test holds it.
        public static T ReadCast<T>(double cast, IReadOnlyList<T> table) => table[(int)(cast * table.Count)];

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
        // text, it fails with no reason given. It binds through CharacterBrief, so a brief missing
        // any field, its name dice included, is refused. Every new save has a name (ADR-009).
        [McpServerTool(Name = "save_character"), Description("Keep a written character, its name, and the brief it was written from.")]
        public static int SaveCharacter(CharacterBrief brief, CharacterName name, string text)
        {
            var all = Storage.Load();

            // One past the highest, not Count + 1: after a hand edit that deletes an entry,
            // Count + 1 could save over the last one.
            var number = all.Count == 0 ? 1 : all.Keys.Max() + 1;

            all[number] = new SavedCharacter(Storage.ToJson(brief), name, text);
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