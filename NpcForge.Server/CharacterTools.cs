using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;

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
            // Required and nullable on purpose (ADR-010). Null is "not given": the scene named no want,
            // and the character is written around winning their goodwill. Required, so a caller must
            // send that null; a forgotten argument is refused, never read as "not given". The console
            // reads required-and-nullable from the schema as "never ask, never default".
            [Description("What the players want from this character, such as the name of a fence")]
            string? playersWant,
            [Description("Who the character is, such as innkeeper or farmer")]
            string occupation,
            // After occupation only because C# puts optional parameters last (CS1737). The brief keeps
            // its own order, so the brief JSON does not move.
            [Description("How hard it is to get what the players want: Easy, SomeWork or Wall")]
            Difficulty? difficulty = null,
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
            NameLetter? familyLetter = null,
            [Description("Whether the character is male or female: Male or Female")]
            Gender? gender = null,
            [Description("How old the character is: Teenager, Adult, MiddleAged or Old")]
            Age? age = null,
            [Description("What stands out about how the character looks, such as bald or a scar across one cheek. Not their people or their age, which are fields of their own")]
            string? look = null)
        {
            // "Not given" has one spelling, null. A blank want would reach the writer as a want of nothing.
            if (playersWant is not null && string.IsNullOrWhiteSpace(playersWant))
                throw new McpException($"{nameof(playersWant)} is blank. Send null when the players want nothing given.");

            // A blank look would reach the writer as a look of nothing. Leaving it out rolls one.
            if (look is not null && string.IsNullOrWhiteSpace(look))
                throw new McpException($"{nameof(look)} is blank. Leave it out to roll one.");

            // The SDK's string-enum converter also accepts a number, so an undefined enum would bind
            // and be written back as that number. Refused here, before any die is cast, by name.
            RequireDefined(nameof(difficulty), difficulty);
            RequireDefined(nameof(obstacle), obstacle);
            RequireDefined(nameof(tone), tone);
            RequireDefined(nameof(nameLetter), nameLetter);
            RequireDefined(nameof(nameSyllables), nameSyllables);
            RequireDefined(nameof(familyLetter), familyLetter);
            RequireDefined(nameof(gender), gender);
            RequireDefined(nameof(age), age);

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
            // The thirteenth die, cast after the twelve so none of them moves (ADR-010). Cast even when a
            // difficulty is handed in, like every other die.
            var rolledDifficulty = Tables.PickWeighted(Tables.Difficulties, Dice);
            // Seven more after the thirteenth, so no older die moves (ADR-011): gender, age, then the look's
            // five casts in the order of LookCasts. All seven are cast here, before any handed-in value is
            // read. The look is read late, from its casts and the settled age, so a handed-in look, age or
            // gender replaces a result and never skips a die.
            var rolledGender = Pick(Enum.GetValues<Gender>());
            var rolledAge = Pick(Enum.GetValues<Age>());
            var lookCasts = new LookCasts(Dice.NextDouble(), Dice.NextDouble(), Dice.NextDouble(), Dice.NextDouble(), Dice.NextDouble());
            var settledDifficulty = difficulty ?? rolledDifficulty;
            var settledAge = age ?? rolledAge;

            // The difficulty filter, in code. Left to the model, it would pick something agreeable.
            // It reads the settled difficulty, rolled or handed in.
            var wants = Tables.Wants.Where(w => w.Fits.Contains(settledDifficulty)).ToArray();

            // Every obstacle except Wont when the difficulty is Easy: "they will not" does not fit
            // an ask that almost any attempt clears.
            ObstacleKind[] obstacles = Enum.GetValues<ObstacleKind>().Where(o => !(settledDifficulty == Difficulty.Easy && o == ObstacleKind.Wont)).ToArray();

            var rolledWants = ReadCast(wantsCast, wants).Text;
            var rolledObstacle = ReadCast(obstacleCast, obstacles);
            var rolledLook = Looks.Text(Looks.Read(lookCasts, settledAge));

            return new CharacterBrief
            {
                Setting = setting,
                PlayersWant = playersWant,
                Difficulty = settledDifficulty,
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
                Gender = gender ?? rolledGender,
                Age = settledAge,
                Look = look ?? rolledLook,
            };
        }

        private static void RequireDefined<T>(string parameter, T? value) where T : struct, Enum
        {
            if (value is { } given && !Enum.IsDefined(given))
                throw new McpException($"{parameter} {Convert.ToInt32(given)} is not one of its values.");
        }

        // The dice: the whole of the variety, and the model cannot reach them. An empty table
        // throws here, and the app's CallDirectAsync turns that into a stopped run.
        private static T Pick<T>(IReadOnlyList<T> table) => table[Dice.Next(table.Count)];

        // The cast and its read live in the core's Dice. DiceOrderTests names this entry in full.
        public static T ReadCast<T>(double cast, IReadOnlyList<T> table) => NpcForge.Dice.ReadCast(cast, table);

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
        // number returns an error result, not a throw: the SDK logs a thrown tool exception at error
        // level with its stack trace, and a mistyped number is the GM's slip, not a crash (ADR-013,
        // decision 6). The console stops the run with the text. The found save is serialized with the
        // SDK's own options, so the console reads the same JSON a plain return value gave it.
        [McpServerTool(Name = "load_character"), Description("Load a kept character by its number.")]
        public static CallToolResult LoadCharacter(int number)
        {
            var all = Storage.Load();

            if (!all.TryGetValue(number, out var saved))
                return new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = $"No character saved as {number}." }],
                };

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = JsonSerializer.Serialize(saved, McpJsonUtilities.DefaultOptions) }],
            };
        }

        // App-only (ADR-013, decision 4). --list calls this to find a save again. Only the name, the
        // occupation and the setting leave the server: the text is the model's prose and is never
        // read here, and the rest of the brief stays in the file. A brief without one of the two
        // keys gives null for it, so the 16 saves from before the namer list as they are.
        [McpServerTool(Name = "list_characters"), Description("List every kept character: its number, name, occupation and setting.")]
        public static List<ListedCharacter> ListCharacters() =>
            [.. Storage.Load().OrderBy(entry => entry.Key).Select(entry => new ListedCharacter(
                entry.Key,
                entry.Value.Name,
                BriefText(entry.Value.Brief, "occupation"),
                BriefText(entry.Value.Brief, "setting")))];

        // The brief is the JSON it was saved as, so a key is read by its saved name, not through
        // CharacterBrief (ADR-009).
        private static string? BriefText(JsonElement brief, string key) =>
            brief.ValueKind == JsonValueKind.Object
            && brief.TryGetProperty(key, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    }

    // One row of the list. The name keeps the shape the save holds, so the console reads it as it
    // reads a loaded save's. The fields keep the brief's own names (ADR-013).
    public sealed record ListedCharacter(int Number, CharacterName? Name, string? Occupation, string? Setting);
}