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
            // The roll lives in the core's Dice. A refusal becomes an MCP error with the same text, so
            // the client reads what it read before the move.
            try
            {
                return ProcessDice.Roll(
                    setting, playersWant, occupation, difficulty, characterWants, obstacle, attitude, mannerism,
                    weakness, willNotDiscuss, wrongAbout, tone, race, nameLetter, nameSyllables, familyLetter,
                    gender, age, look);
            }
            catch (RollRefusedException refused)
            {
                throw new McpException(refused.Reason);
            }
        }

        // The cast and its read live in the core's Dice. DiceOrderTests names this entry in full.
        public static T ReadCast<T>(double cast, IReadOnlyList<T> table) => NpcForge.Dice.ReadCast(cast, table);

        // With NPCFORGE_SEED set, the same seed rolls the same brief every time, because Dice.Roll
        // always casts in the same order. Without it, Random.Shared, as before. Made once per server
        // process, and the console starts a new server for every run.
        private static readonly Dice ProcessDice = CreateDice();

        private static Dice CreateDice()
        {
            var seed = Environment.GetEnvironmentVariable("NPCFORGE_SEED");

            // stderr, never stdout: stdout is the wire. The console shows it as [server] dice ...
            Console.Error.WriteLine(seed is null ? "dice: random" : $"dice: seed {seed}");

            return new Dice(seed is null ? null : int.Parse(seed));
        }

        // App-only. The app saves every run once the model has written. Returns the new number.
        // The brief must arrive as a JSON object, not as the text the roll returned: sent as
        // text, it fails with no reason given. It binds through CharacterBrief, so a brief missing
        // any field, its name dice included, is refused. Every new save has a name (ADR-009).
        [McpServerTool(Name = "save_character"), Description("Keep a written character, its name, and the brief it was written from.")]
        public static int SaveCharacter(CharacterBrief brief, CharacterName name, string text) =>
            Await(Store().SaveAsync(SavedCharacter.Of(brief, name, text), CancellationToken.None));

        // App-only. --load calls this instead of rolling and instead of the model. An unknown
        // number returns an error result, not a throw: the SDK logs a thrown tool exception at error
        // level with its stack trace, and a mistyped number is the GM's slip, not a crash (ADR-013,
        // decision 6). The console stops the run with the text. The found save is serialized with the
        // SDK's own options, so the console reads the same JSON a plain return value gave it.
        [McpServerTool(Name = "load_character"), Description("Load a kept character by its number.")]
        public static CallToolResult LoadCharacter(int number)
        {
            var saved = Await(Store().LoadAsync(number, CancellationToken.None));

            if (saved is null)
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
        // read here, and the rest of the brief stays in the file. ListedCharacter.Of reads them.
        [McpServerTool(Name = "list_characters"), Description("List every kept character: its number, name, occupation and setting.")]
        public static List<ListedCharacter> ListCharacters() =>
            [.. Await(Store().ListAsync(CancellationToken.None))];

        // Save, load and list are one call each into the store (ADR-014, decision 9). Storage reads
        // NPCFORGE_CHARACTERS on every call, so a store is built per call on its path.
        private static FileCharacterStore Store() => new(Storage.FilePath);

        // These tools are sync and the SDK calls them as they are, so each blocks on the store's Task.
        // A file store's Task is complete when it comes back, so nothing can deadlock. The server
        // retires in the behavior PR and the console then awaits the store itself (ADR-014).
        private static T Await<T>(Task<T> task) => task.GetAwaiter().GetResult();
    }
}