namespace NpcForge;

// The dice: the whole of the variety, and the model cannot reach them. One Dice is one run's dice. A seed
// gives the same brief every time, because Roll always casts in the same order. No seed gives
// Random.Shared, as before.
public sealed class Dice(int? seed)
{
    private readonly Random _random = seed is null ? Random.Shared : new Random(seed.Value);

    // Rolls up a character brief inside what the difficulty allows. Throws RollRefusedException, before
    // any die is cast, for a blank field or an undefined enum. The parameters are the ones the MCP tool
    // roll_character has, and the descriptions the interview and the model's tool list read stay on that
    // tool until the console calls the core in-process (ADR-014).
    public CharacterBrief Roll(
        string setting,
        string? playersWant,
        string occupation,
        Difficulty? difficulty = null,
        string? characterWants = null,
        ObstacleKind? obstacle = null,
        string? attitude = null,
        string? mannerism = null,
        string? weakness = null,
        string? willNotDiscuss = null,
        string? wrongAbout = null,
        Tone? tone = null,
        string? race = null,
        NameLetter? nameLetter = null,
        Syllables? nameSyllables = null,
        NameLetter? familyLetter = null,
        Gender? gender = null,
        Age? age = null,
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
