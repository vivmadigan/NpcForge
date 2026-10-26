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
    // RollParameter: the roll_character parameter this answer fills, null for a flag that is not
    // an answer. The order of this table is the order of the arguments.
    private sealed record Flag(
        string Name,
        string? Default,
        Func<string, string> Read,
        bool Recorded = false,
        string? MissingValue = null,
        string? RollParameter = null);

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
        new("--setting", "an inn in a major city", AsIs, Recorded: true, RollParameter: "setting"),
        new("--want", "the name of a fence", AsIs, Recorded: true, RollParameter: "playersWant"),
        new("--difficulty", "Wall", AsIs, Recorded: true, RollParameter: "difficulty"),
        new("--occupation", "innkeeper", AsIs, Recorded: true, RollParameter: "occupation"),
        // ADR-009: no default. Left off the line, the dice roll them; a flag given is handed to
        // roll_character and recorded. The server reads a name or a letter in any case.
        new("--tone", null, AsIs, Recorded: true, RollParameter: "tone"),
        new("--race", null, AsIs, Recorded: true, RollParameter: "race"),
        new("--letter", null, AsIs, Recorded: true, RollParameter: "nameLetter"),
        new("--syllables", null, Number, Recorded: true, RollParameter: "nameSyllables"),
        new("--family-letter", null, AsIs, Recorded: true, RollParameter: "familyLetter"),
        // --load <number> prints a saved character instead of making a new one.
        new("--load", null, Number,
            MissingValue: "--load needs a number, for example --load 3."),
        // --record <file> also writes every model call to a file.
        new("--record", null, AsIs,
            MissingValue: "--record needs a file, for example --record NpcForge.Tests/Fixtures/replay.jsonl."),
        // --recording <file> is the file the replay provider answers from.
        new("--recording", null, AsIs,
            MissingValue: "--recording needs a file, for example --recording NpcForge.Tests/Fixtures/replay.jsonl."),
        // The writers' lab (ADR-009). None of these is an answer, and none goes on line 1 of a
        // recording: a lab is not recorded. Folders are read as typed, not lower-cased.
        new("--lab", null, AsIs,
            MissingValue: "--lab needs an axis and its values, for example --lab skill=skills/a,skills/b."),
        new("--repeat", "1", Number,
            MissingValue: "--repeat needs a number, for example --repeat 2."),
        new("--mode", "side", Lower),
        new("--out", null, AsIs,
            MissingValue: "--out needs a folder, for example --out lab-1."),
        new("--score", null, AsIs,
            MissingValue: "--score needs the folder of a blind lab, for example --score lab-1."),
    ];

    private readonly Dictionary<string, string> _values;

    // The flags typed on the command line, as opposed to the defaults filled in for the rest.
    private readonly HashSet<string> _given;

    private RunOptions(Dictionary<string, string> values, HashSet<string> given)
    {
        _values = values;
        _given = given;
    }

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

    // The lab: null for a normal run. Parsed from the flag each time, and already proved good by
    // RequireFlagsThatGoTogether, so a caller never sees a half-valid plan.
    public LabPlan? Lab => _values.TryGetValue("--lab", out var text) ? LabPlan.Parse(text) : null;
    public int Repeat => int.Parse(_values["--repeat"]);
    public string Mode => _values["--mode"];
    public string? Out => _values.GetValueOrDefault("--out");
    public string? Score => _values.GetValueOrDefault("--score");

    private int? Int(string name) => _values.TryGetValue(name, out var value) ? int.Parse(value) : null;

    // No answer flag and no --load: the GM describes the scene instead (ADR-007). Any answer flag
    // skips the interview, so a run with flags, the replay test's among them, is the run it was.
    public bool Interview => Load is null && !AnyAnswerGiven;

    private bool AnyAnswerGiven => Table.Any(flag => flag.RollParameter is not null && _given.Contains(flag.Name));

    public static RunOptions Parse(string[] args)
    {
        var values = Table.Where(flag => flag.Default is not null)
            .ToDictionary(flag => flag.Name, flag => flag.Default!);
        var given = new HashSet<string>();

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
            given.Add(flag.Name);
        }

        var options = new RunOptions(values, given);
        options.RequireFlagsThatGoTogether();
        return options;
    }

    // Listed from the table, so the message cannot name a flag the parser does not have.
    private static string KnownFlags() => "The flags are: " + string.Join(", ", Table.Select(flag => flag.Name)) + ".";

    // All of these stop the run before anything is paid for.
    private void RequireFlagsThatGoTogether()
    {
        RequireLabFlagsThatGoTogether();

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

        // Line 1 of a recording is the run's answers as flags, and a replay cannot type a scene, so
        // an interview could never be replayed. Refused before the GM types anything (ADR-007).
        if (RecordTo is not null && !AnyAnswerGiven)
            throw new ArgumentException(
                "--record needs the answers as flags (--setting, --want, --difficulty or --occupation): " +
                "a recording replays flags, and a described scene is not one.");
    }

    // The lab's refusals (ADR-009 section 5), each with its own reason. Same stop as the others: before
    // a model client is built, so a lab that cannot run costs nothing.
    private void RequireLabFlagsThatGoTogether()
    {
        // --score reads a finished folder. Any other flag beside it would be silently ignored, and a
        // score that ignores a flag is a run the GM did not ask for.
        if (_given.Contains("--score"))
        {
            var others = _given.Where(name => name != "--score").ToList();
            if (others.Count > 0)
                throw new ArgumentException(
                    $"--score takes no other flag, and {string.Join(", ", others)} was given: " +
                    "it reads a finished blind lab and calls no model.");
            return;
        }

        if (!_given.Contains("--lab"))
        {
            foreach (var name in new[] { "--repeat", "--mode", "--out" })
                if (_given.Contains(name))
                    throw new ArgumentException($"{name} only goes with --lab.");
            return;
        }

        if (Repeat < 1)
            throw new ArgumentException("--repeat needs a number of 1 or more.");

        if (Mode is not ("side" or "blind"))
            throw new ArgumentException($"--mode is side or blind, not '{Mode}'.");

        // Throws its own reason for a bad axis, a repeated skill folder or fewer than two values.
        var plan = LabPlan.Parse(_values["--lab"]);

        // The lab sets the tone of every variant. A tone given as well would be ignored, which is a
        // lab the GM did not ask for.
        if (plan.Axis == "tone" && _given.Contains("--tone"))
            throw new ArgumentException(
                "--lab tone cannot go with --tone: the lab sets each variant's tone, so a tone given as well would be ignored.");

        // Every variant must be written from one brief, and a seed is what makes the roll repeatable.
        if (Seed is null)
            throw new ArgumentException("--lab needs --seed, for example --seed 1: every variant is written from the same brief.");

        // An interview cannot be confirmed before the model is called, and cannot be repeated.
        if (!AnyAnswerGiven)
            throw new ArgumentException(
                "--lab needs the answers as flags (--setting, --want, --difficulty or --occupation): " +
                "an interview cannot be repeated, and a lab of a typed scene waits for a later epic.");

        if (Load is not null)
            throw new ArgumentException("--lab cannot go with --load: a lab writes new characters and loads none.");

        if (RecordTo is not null)
            throw new ArgumentException("--lab cannot go with --record: a lab is compared on its sheet and is never recorded.");

        if (Provider == "replay")
            throw new ArgumentException("--lab cannot go with --provider replay: a replay answers from one recording, so every variant would read the same.");

        // claude-opus-5 and claude-sonnet-5 reject a temperature with a 400 (see Program.cs), so the
        // whole lab would be paid for and fail.
        if (plan.Axis == "temperature" && Provider == "anthropic")
            throw new ArgumentException("--lab temperature cannot go with --provider anthropic: its models reject a temperature with a 400.");

        // A blind sheet letters its characters A to Z.
        if (Mode == "blind" && plan.Variants.Count * Repeat > LabSheet.MostBlindCharacters)
            throw new ArgumentException(
                $"--mode blind letters its characters A to Z, so it writes at most {LabSheet.MostBlindCharacters}: " +
                $"{plan.Variants.Count} variants x {Repeat} takes is too many.");
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

        // A lab saves nothing. This is the second, independent stop behind that: even a bug in the
        // lab could only write a temp file, never the owner's characters.json.
        if (Provider != "replay" && RecordTo is null && Lab is null)
            return null;

        var kind = RecordTo is not null ? "record" : Lab is not null ? "lab" : "replay";
        return Path.Combine(Path.GetTempPath(), $"npcforge-{kind}-{Guid.NewGuid():N}.json");
    }

    // The arguments of the roll_character call, named by the table. Seed is not among them: it goes
    // to the server, not to the tool. A flag with no default and no value is left out, so the
    // server's dice roll it (ADR-009).
    public Dictionary<string, object?> RollArguments() =>
        Table.Where(flag => flag.RollParameter is not null && _values.ContainsKey(flag.Name))
            .ToDictionary(flag => flag.RollParameter!, flag => (object?)_values[flag.Name]);

    // Line 1 of a recording: this run's answers as flags, the values actually used and not what was
    // typed, so a default that changes later cannot change what the replay test runs. No --provider
    // and no --record among them: the replay test adds its own --provider. A flag with no value is
    // left out, as it is from the roll, so a recording of a run that rolled its race replays rolling it.
    public string[] RecordedFlags() =>
        [.. Table.Where(flag => flag.Recorded && _values.ContainsKey(flag.Name))
            .SelectMany(flag => new[] { flag.Name, _values[flag.Name] })];
}
