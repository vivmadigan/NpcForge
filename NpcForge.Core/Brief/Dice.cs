using Microsoft.Extensions.AI;
using System.ComponentModel;            // Description
using System.Reflection;                // MethodInfo

namespace NpcForge;

// The dice: the whole of the variety, and the model cannot reach them. One Dice is one run's dice. A seed
// gives the same brief every time, because Roll always casts in the same order. No seed gives
// Random.Shared, as before.
public sealed class Dice(int? seed)
{
    private readonly Random _random = seed is null ? Random.Shared : new Random(seed.Value);

    // roll_character as the MCP SDK describes it: AIFunctionFactory over the typed Roll below. It is never
    // offered to a model and never called as a tool. Fields reads its schema and Roll(arguments) binds a
    // dictionary through it, so the field list and the binding both come from the one set of [Description]s
    // on Roll (ADR-014, decisions 3 and 5). Built once for every Dice, so the instance whose dice are cast
    // travels in the call's Context. The result is handed back as the CharacterBrief it is, not as JSON.
    private static readonly AIFunction RollFunction = AIFunctionFactory.Create(
        // The typed Roll is the only overload with more than one parameter.
        typeof(Dice).GetMethods().Single(method => method.Name == nameof(Roll) && method.GetParameters().Length > 1),
        call => (Dice)call.Context![typeof(Dice)]!,
        new AIFunctionFactoryOptions
        {
            Name = "roll_character",
            MarshalResult = (result, _, _) => new ValueTask<object?>(result),
        });

    // roll_character's fields, read from that schema. Once the console calls the core in-process (ADR-014),
    // the interview, ToneFiles and SavedView read this. Until then they read the server's copy of the same
    // descriptions, which CoreMatchesServerTests holds equal to it.
    public static IReadOnlyList<RollField> Fields { get; } = RollField.ListFrom(RollFunction.JsonSchema);

    // The roll with its arguments as a dictionary, parameter name to value, the way the console and a lab
    // hold them. Binds by name through AIFunctionFactory over this class's own Roll, the factory the MCP SDK
    // binds through for the server, then casts the same dice in the same order. A blank want or look and an
    // undefined enum number refuse as RollRefusedException, as on the server. A missing argument, or an enum
    // name such as "grumpy", fails in the binder with its own ArgumentException instead. ADR-014, decision 3,
    // wants every refusal to be a RollRefusedException, and the switch settles that, together with the text
    // the GM sees. Wrapping every ArgumentException here would also relabel an internal bug, such as an empty
    // table, as a refusal. The call completes before it returns, because Roll does no IO, so blocking on it
    // cannot deadlock.
    public CharacterBrief Roll(IReadOnlyDictionary<string, object?> arguments)
    {
        var call = new AIFunctionArguments(new Dictionary<string, object?>(arguments))
        {
            Context = new Dictionary<object, object?> { [typeof(Dice)] = this },
        };
        return (CharacterBrief)RollFunction.InvokeAsync(call).AsTask().GetAwaiter().GetResult()!;
    }

    // Rolls up a character brief inside what the difficulty allows. Throws RollRefusedException, before
    // any die is cast, for a blank field or an undefined enum. Each parameter's [Description] is what the
    // scene interview's model reads about that field. The server keeps a copy of them until it retires.
    [Description("Roll up a character brief inside what the difficulty allows.")]
    public CharacterBrief Roll(
        [Description("Where the players meet this character, such as an inn in a major city")]
        string setting,
        // Required and nullable on purpose (ADR-010). Null is "not given": the scene named no want, and
        // the character is written around winning their goodwill. Required, so a caller must send that
        // null; a forgotten argument is refused, never read as "not given". The interview reads
        // required-and-nullable from the schema as "never ask, never default".
        [Description("What the players want from this character, such as the name of a fence")]
        string? playersWant,
        [Description("Who the character is, such as innkeeper or farmer")]
        string occupation,
        // After occupation only because C# puts optional parameters last (CS1737). The brief keeps its own
        // order, so the brief JSON does not move.
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
            throw new RollRefusedException($"{nameof(playersWant)} is blank. Send null when the players want nothing given.");

        // A blank look would reach the writer as a look of nothing. Leaving it out rolls one.
        if (look is not null && string.IsNullOrWhiteSpace(look))
            throw new RollRefusedException($"{nameof(look)} is blank. Leave it out to roll one.");

        // The MCP SDK's string-enum converter also accepts a number, so an undefined enum would bind
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
        var wantsCast = _random.NextDouble();
        var obstacleCast = _random.NextDouble();
        var rolledAttitude = Pick(Tables.Attitudes);
        var rolledMannerism = Pick(Tables.Mannerisms);
        var rolledWeakness = Pick(Tables.Weaknesses);
        var rolledWillNotDiscuss = Pick(Tables.WillNotDiscuss);
        var rolledWrongAbout = Pick(Tables.WrongAbout);
        var rolledTone = Pick(Enum.GetValues<Tone>());
        var rolledRace = Tables.PickWeighted(Tables.Races, _random);
        var rolledNameLetter = Tables.PickWeighted(Tables.NameLetters, _random);
        var rolledNameSyllables = Tables.PickWeighted(Tables.SyllableCounts, _random);
        var rolledFamilyLetter = Tables.PickWeighted(Tables.NameLetters, _random);
        // The thirteenth die, cast after the twelve so none of them moves (ADR-010). Cast even when a
        // difficulty is handed in, like every other die.
        var rolledDifficulty = Tables.PickWeighted(Tables.Difficulties, _random);
        // Seven more after the thirteenth, so no older die moves (ADR-011): gender, age, then the look's
        // five casts in the order of LookCasts. All seven are cast here, before any handed-in value is
        // read. The look is read late, from its casts and the settled age, so a handed-in look, age or
        // gender replaces a result and never skips a die.
        var rolledGender = Pick(Enum.GetValues<Gender>());
        var rolledAge = Pick(Enum.GetValues<Age>());
        var lookCasts = new LookCasts(_random.NextDouble(), _random.NextDouble(), _random.NextDouble(), _random.NextDouble(), _random.NextDouble());
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
            throw new RollRefusedException($"{parameter} {Convert.ToInt32(given)} is not one of its values.");
    }

    // An empty table throws here, and the app's CallDirectAsync turns that into a stopped run.
    private T Pick<T>(IReadOnlyList<T> table) => table[_random.Next(table.Count)];

    // A die cast now, as NextDouble(), and read later against its table. A seeded Random computes
    // Next(n) as (int)(NextDouble() * n), so this picks the row Pick would have picked had the table
    // been known at the cast, and every seed keeps its brief (ADR-010). A test holds it.
    public static T ReadCast<T>(double cast, IReadOnlyList<T> table) => table[(int)(cast * table.Count)];
}
