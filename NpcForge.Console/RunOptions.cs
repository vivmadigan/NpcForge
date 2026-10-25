namespace NpcForge;

// The run's answers, read from the command line. One table says which flags exist, what each
// defaults to, how its value is read and whether it goes on line 1 of a recording, so the parser
// and the recording cannot drift: add a flag here and both know it.
public sealed class RunOptions
{
    // MissingValue: the refusal for a flag given with no value after it. Null gets the plain
    // message below. A flag silently left at its default would be a paid run that is not the one
    // the GM asked for (or one that records nothing).
    // Recorded: written to line 1 of a recording. The order of this table is the order on that line.
    private sealed record Flag(
        string Name,
        string? Default,
        Func<string, string> Read,
        bool Recorded = false,
        string? MissingValue = null);

    private static string AsIs(string value) => value;
    private static string Lower(string value) => value.ToLower();
    // Parsed now, so a value that is not a number stops the run here, and kept as text.
    private static string Number(string value) => int.Parse(value).ToString();

    private static readonly Flag[] Table =
    [
        new("--provider", "openai", Lower),
        new("--seed", null, Number, Recorded: true,
            MissingValue: "--seed needs a number, for example --seed 1."),
        new("--model", "gpt-6-luna", Lower, Recorded: true),
        // Occupation sits with the setting because they change together: a farm run wants
        // --occupation as well as --setting.
        new("--setting", "an inn in a major city", AsIs, Recorded: true),
        new("--want", "the name of a fence", AsIs, Recorded: true),
        new("--difficulty", "Wall", AsIs, Recorded: true),
        new("--occupation", "innkeeper", AsIs, Recorded: true),
        // --load <number> prints a saved character instead of making a new one.
        new("--load", null, Number,
            MissingValue: "--load needs a number, for example --load 3."),
        // --record <file> also writes every model call to a file.
        new("--record", null, AsIs,
            MissingValue: "--record needs a file, for example --record NpcForge.Tests/Fixtures/replay.jsonl."),
        // --recording <file> is the file the replay provider answers from.
        new("--recording", null, AsIs,
            MissingValue: "--recording needs a file, for example --recording NpcForge.Tests/Fixtures/replay.jsonl."),
    ];

    private readonly Dictionary<string, string> _values;

    private RunOptions(Dictionary<string, string> values) => _values = values;

    public string Provider => _values["--provider"];
    public string Model => _values["--model"];
    public string Setting => _values["--setting"];
    public string Want => _values["--want"];
    public string Difficulty => _values["--difficulty"];
    public string Occupation => _values["--occupation"];

    // --seed <number> fixes the dice. It goes to the server, never to roll_character, whose
    // parameters are only the brief fields a GM may set.
    public int? Seed => Int("--seed");
    public int? Load => Int("--load");
    public string? RecordTo => _values.GetValueOrDefault("--record");
    public string? Recording => _values.GetValueOrDefault("--recording");

    private int? Int(string name) => _values.TryGetValue(name, out var value) ? int.Parse(value) : null;

    public static RunOptions Parse(string[] args)
    {
        var values = Table.Where(flag => flag.Default is not null)
            .ToDictionary(flag => flag.Name, flag => flag.Default!);

        // Every argument is a flag or the value of the flag before it, and anything else stops the
        // run here, before a model client is built. A value is consumed with its flag, so it is
        // never read again as a flag: `--setting --load 3` is a --setting with no value.
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--"))
                throw new ArgumentException(
                    $"Unexpected argument '{args[i]}': a flag starts with -- and is followed by its value. {KnownFlags()}");

            var flag = Array.Find(Table, f => f.Name == args[i])
                ?? throw new ArgumentException($"Unknown flag {args[i]}. {KnownFlags()}");

            // The next argument being a flag means this one was given no value, not that the value
            // is called "--something".
            if (i + 1 == args.Length || args[i + 1].StartsWith("--"))
                throw new ArgumentException(flag.MissingValue ?? $"{flag.Name} needs a value after it.");

            values[flag.Name] = flag.Read(args[++i]);
        }

        var options = new RunOptions(values);
        options.RequireFlagsThatGoTogether();
        return options;
    }

    // Listed from the table, so the message cannot name a flag the parser does not have.
    private static string KnownFlags() => "The flags are: " + string.Join(", ", Table.Select(flag => flag.Name)) + ".";

    // All of these stop the run before anything is paid for.
    private void RequireFlagsThatGoTogether()
    {
        // A recording without a seed could never replay: the dice would roll a different brief, so
        // the first message would differ.
        if (RecordTo is not null && Seed is null)
            throw new ArgumentException("--record needs --seed, for example --seed 1.");

        // --recording on its own would leave the provider on openai: a paid run that ignores the file.
        if (Recording is not null && Provider != "replay")
            throw new ArgumentException("--recording only goes with --provider replay.");

        // A replay must never write a recording, least of all over the one it is reading.
        if (RecordTo is not null && Provider == "replay")
            throw new ArgumentException("--record cannot go with --provider replay: a replay never writes a recording.");
    }

    // Which file this run saves its character in. A replay or a recording run is not a real
    // character, so unless the caller says otherwise it goes in a fresh temp file and the tracked
    // characters.json stays the owner's. A normal run and --load keep the tracked file (null leaves
    // the server on its own default). `told` is NPCFORGE_CHARACTERS as the caller set it, and
    // always wins: the tests name their own file. Either way the answer is returned, so the
    // console can say where it saved.
    public string? CharactersPath(string? told)
    {
        if (!string.IsNullOrEmpty(told))
            return told;

        if (Provider != "replay" && RecordTo is null)
            return null;

        var kind = RecordTo is not null ? "record" : "replay";
        return Path.Combine(Path.GetTempPath(), $"npcforge-{kind}-{Guid.NewGuid():N}.json");
    }

    // Line 1 of a recording: this run's answers as flags, the values actually used and not what was
    // typed, so a default that changes later cannot change what the replay test runs. No --provider
    // and no --record among them: the replay test adds its own --provider.
    public string[] RecordedFlags() =>
        [.. Table.Where(flag => flag.Recorded).SelectMany(flag => new[] { flag.Name, _values[flag.Name] })];
}
