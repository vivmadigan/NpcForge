namespace NpcForge;

// The run's answers, read from the command line. One table says which flags exist, what each
// defaults to, how its value is read and whether it goes on line 1 of a recording, so the parser
// and the recording cannot drift: add a flag here and both know it.
public sealed class RunOptions
{
    // MissingValue: the refusal for a flag given as the last argument. Null means the flag is
    // ignored then and its default stays, which is what a bare run has always done. A flag with a
    // message here would otherwise fall through to a paid run (or a paid run that records nothing).
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
        new("--seed", null, Number, Recorded: true),
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
        new("--recording", null, AsIs),
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

        // A value is read again as a flag on the next pass, as it always has been: after
        // `--setting --load`, --load is still seen. Unknown flags are skipped.
        for (int i = 0; i < args.Length; i++)
        {
            var flag = Array.Find(Table, f => f.Name == args[i]);
            if (flag is null)
                continue;

            if (i + 1 == args.Length)
            {
                if (flag.MissingValue is not null)
                    throw new ArgumentException(flag.MissingValue);
                continue;
            }

            values[flag.Name] = flag.Read(args[i + 1]);
        }

        var options = new RunOptions(values);
        options.RequireFlagsThatGoTogether();
        return options;
    }

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

    // Line 1 of a recording: this run's answers as flags, the values actually used and not what was
    // typed, so a default that changes later cannot change what the replay test runs. No --provider
    // and no --record among them: the replay test adds its own --provider.
    public string[] RecordedFlags() =>
        [.. Table.Where(flag => flag.Recorded).SelectMany(flag => new[] { flag.Name, _values[flag.Name] })];
}
