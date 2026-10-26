using Microsoft.Extensions.AI;
using System.Globalization;             // CultureInfo, NumberStyles

namespace NpcForge;

// One thing a lab changes. A skill variant changes the writer's skill folder; a temperature variant
// changes ChatOptions.Temperature; a tone variant changes the brief's tone. Exactly one of the three
// is set, and nothing else differs between variants (ADR-009).
public sealed record LabVariant(string Label, string? SkillDir, float? Temperature, string? Tone = null);

// What --lab says: one axis and the values to compare on it. Parsed in one place so the flag parser
// can refuse a bad one before any model client is built, and the lab reads the same plan after.
// A tone plan with no variants means "every tone the server has": the schema says which, and the
// parser has not seen it, so the lab fills them in.
public sealed record LabPlan(string Axis, IReadOnlyList<LabVariant> Variants)
{
    private static readonly string[] Axes = ["skill", "temperature", "tone"];

    public static LabPlan Parse(string text)
    {
        var at = text.IndexOf('=');
        var axis = (at < 0 ? text : text[..at]).Trim().ToLowerInvariant();

        if (!Axes.Contains(axis))
            throw new ArgumentException($"Unknown lab axis '{axis}'. The axes are: {string.Join(", ", Axes)}.");

        // Folder paths are read as typed, so a comma cannot be part of one. A blank entry is dropped,
        // so a trailing comma does not make a variant out of nothing.
        var values = at < 0
            ? []
            : text[(at + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // No values is every tone, which is a comparison already. Any other axis, or a tone list, needs
        // two things to differ.
        if (values.Length < 2 && !(axis == "tone" && values.Length == 0))
            throw new ArgumentException(
                $"--lab {axis} needs at least two values to compare, for example --lab {Example(axis)}.");

        return axis switch
        {
            "skill" => SkillVariants(values),
            "tone" => ToneVariants(values),
            _ => TemperatureVariants(values),
        };
    }

    private static string Example(string axis) => axis switch
    {
        "skill" => "skill=skills/a,skills/b",
        "tone" => "tone=Comic,Serious",
        _ => "temperature=0.2,0.9",
    };

    // Typed names, checked against the schema's tones by the lab once the server is connected.
    private static LabPlan ToneVariants(string[] names)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
            if (!seen.Add(name))
                throw new ArgumentException($"--lab tone names {name} twice: each variant needs its own tone.");
        return new LabPlan("tone", [.. names.Select(name => new LabVariant(name, null, null, name))]);
    }

    private static LabPlan SkillVariants(string[] folders)
    {
        // The same folder twice is the same writer twice: a sheet of two identical columns that
        // costs double. Compared by full path, so "a" and "./a" are one folder.
        var full = folders.Select(folder => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder))).ToList();
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var path in full)
            if (!seen.Add(path))
                throw new ArgumentException($"--lab skill names the folder {path} twice: each variant needs its own skill folder.");

        // The folder's own name is the label, short enough to type at --score. Two folders with
        // the same name in different places fall back to the whole path. Names are compared
        // ignoring case, because --score does: x/A and y/a would both match the guess "a".
        var names = full.Select(Path.GetFileName).ToList();
        var variants = full.Select((path, i) => new LabVariant(
            names.Count(name => string.Equals(name, names[i], StringComparison.OrdinalIgnoreCase)) > 1 ? path : names[i]!,
            path, null));
        return new LabPlan("skill", [.. variants]);
    }

    private static LabPlan TemperatureVariants(string[] values)
    {
        // Compared as numbers, so 0.5 and 0.50 are one temperature written twice.
        var seen = new HashSet<float>();
        var variants = values.Select(value =>
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t)
                || !float.IsFinite(t) || t < 0)
                throw new ArgumentException($"--lab temperature needs numbers of 0 or more, and '{value}' is not one.");
            if (!seen.Add(t))
                throw new ArgumentException($"--lab temperature names {value} twice: each variant needs its own temperature.");
            return new LabVariant(value, null, t);
        });
        return new LabPlan("temperature", [.. variants]);
    }
}

// The writers' lab (ADR-009 section 5): one brief, rolled once, written through every variant by the
// same ChatAgent.WriteAsync a normal run uses, then laid out as a sheet. Paid, so it asks first.
public static class Lab
{
    // One character to write: a variant and which take of it.
    private sealed record Planned(LabVariant Variant, int Take, string BriefJson, string SystemText, string SkillLine);

    // --score: reads a finished blind lab, asks for a guess per letter, and writes scored.md. It
    // needs no model, no key and no server, so Program.cs runs it before any of them exist. The
    // dialogue goes to `dialogue` (stderr) and the result to `result` (stdout), like a run.
    public static async Task ScoreAsync(
        string folder, TextReader input, TextWriter dialogue, TextWriter result, CancellationToken ct)
    {
        // blind.json, not blind.md: the sheet is read as data, so a character's own text can never be
        // taken for a heading.
        var blindPath = Path.Combine(folder, "blind.json");
        var keyPath = Path.Combine(folder, "key.json");
        if (!File.Exists(blindPath) || !File.Exists(keyPath))
            throw new InvalidOperationException(
                $"--score needs a blind lab: {folder} must hold blind.json and key.json (made by --lab with --mode blind).");

        var blind = await File.ReadAllTextAsync(blindPath, ct);
        var key = LabSheet.ReadKey(await File.ReadAllTextAsync(keyPath, ct));

        dialogue.WriteLine($"[score] Guess who wrote each character. The labels are: {string.Join(", ", key.Values.Distinct().Order())}.");
        var guesses = new Dictionary<string, string>();
        foreach (var letter in key.Keys)
        {
            dialogue.Write($"[score] Who wrote {letter}? ");
            // End of input leaves the rest unguessed, which scores as wrong.
            guesses[letter] = input.ReadLine()?.Trim() ?? "";
        }
        dialogue.WriteLine();

        var (right, total, scored) = LabSheet.Score(blind, key, guesses);
        await File.WriteAllTextAsync(Path.Combine(folder, "scored.md"), scored, ct);
        result.WriteLine($"{right} of {total} right");
        dialogue.WriteLine($"[score] written to {Path.Combine(folder, "scored.md")}");
    }

    // The tone axis, filled in from the schema: no names given means every tone the server has, and a
    // name is matched to the schema's own spelling, so --lab tone=comic labels its variant Comic.
    private static List<LabVariant> ToneVariants(LabPlan plan, IReadOnlyList<RollField> fields)
    {
        var tones = fields.Single(field => field.Name == "tone").Choices!;
        if (plan.Variants.Count == 0)
            return [.. tones.Select(tone => new LabVariant(tone, null, null, tone))];

        return [.. plan.Variants.Select(variant =>
        {
            var tone = tones.FirstOrDefault(t => t.Equals(variant.Label, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException(
                    $"--lab tone names '{variant.Label}', which is not a tone. The tones are: {string.Join(", ", tones)}.");
            return new LabVariant(tone, null, null, tone);
        })];
    }

    // Returns the folder the sheet is in, or null when the lab stopped before any model call.
    public static async Task<string?> RunAsync(
        IChatClient client, ChatOptions options, McpToolSource tools, RunOptions run, TextReader input, CancellationToken ct)
    {
        var plan = run.Lab ?? throw new ArgumentException("Lab.RunAsync needs a --lab run.");
        var blind = run.Mode == "blind";

        // Every skill folder the lab will use, the default one included, must hold SKILL.md and a
        // file for every tone, so a gap stops the lab here, before its question and before a paid
        // call (ADR-009). A missing folder is reported as the missing SKILL.md it is.
        var skillDirs = plan.Variants.Select(variant => variant.SkillDir).Append(null)
            .Select(dir => dir ?? WriterInput.DefaultSkillDir).Distinct().ToList();
        foreach (var dir in skillDirs)
            if (!File.Exists(Path.Combine(dir, "SKILL.md")))
                throw new FileNotFoundException(
                    $"The skill {Path.Combine(dir, "SKILL.md")} is missing.", Path.Combine(dir, "SKILL.md"));
        var fields = RollField.ListFrom(tools.SchemaOf("roll_character"));
        ToneFiles.Require(fields, skillDirs);

        var variants = plan.Axis == "tone" ? ToneVariants(plan, fields) : plan.Variants;

        var folder = run.Out ?? Path.Combine(
            Path.GetTempPath(), $"npcforge-lab-{DateTime.Now:yyyyMMdd-HHmmss}-{plan.Axis}");

        // Made now, before the question, so a --out that cannot be used (an existing file, a path
        // that is not writable) fails while the lab is still free. The probe write is what finds
        // a folder that exists but cannot be written to. A folder this call made is removed again
        // if the lab stops before writing anything.
        var madeFolder = !Directory.Exists(folder);
        Directory.CreateDirectory(folder);
        var probe = Path.Combine(folder, $".npcforge-probe-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(probe, "", ct);
        File.Delete(probe);

        // A second sheet beside an old one would put a sheet.md next to a blind.md, and a sheet.md
        // beside a blind lab gives away its key.
        if (new[] { "sheet.md", "blind.md", "blind.json", "key.json", "scored.md" }.Any(name => File.Exists(Path.Combine(folder, name))))
        {
            Console.Error.WriteLine($"[lab] not run: {folder} already holds a lab. Name a new --out folder.");
            return null;                // an old lab's folder is not ours to remove
        }

        void RemoveIfMade()
        {
            if (madeFolder)
                Directory.Delete(folder);
        }

        // The roll is free, and the system message needs the brief's tone. The seed is the server's,
        // so every variant is written from this one brief.
        var briefJson = await tools.CallDirectAsync("roll_character", run.RollArguments(), ct);

        // A tone variant's brief is the same brief with its tone replaced, rolled by the server with
        // every field handed back in: the console never writes a brief, and no die lands differently.
        var briefs = new Dictionary<string, string>();
        foreach (var variant in variants)
            briefs[variant.Label] = variant.Tone is { } tone
                ? await tools.CallDirectAsync("roll_character", BriefJson.WithField(briefJson, "tone", tone), ct)
                : briefJson;

        // Every skill is read now, so an unreadable one stops the lab here and not after the first
        // paid call. Temperature variants all use the bundled skill.
        var planned = variants
            .SelectMany(variant => Enumerable.Range(1, run.Repeat).Select(take =>
            {
                var system = ChatAgent.SystemMessage(variant.SkillDir ?? WriterInput.DefaultSkillDir, briefs[variant.Label]);
                return new Planned(variant, take, briefs[variant.Label], system.Text, system.Trace);
            }))
            .ToList();

        // It asks first (costly-steps playbook). Two calls per character is the usual case (one to
        // call lookup_archetype, one to write), each costing its whole input plus the most the model
        // may answer. The writer loop allows more turns than that, and characters stand in for
        // tokens, so this is an estimate and not a bound. It says "about", and the spending cap is
        // what stops a lab that runs over. Tokens only, no prices.
        var estimate = planned.Sum(p =>
            2L * (p.SystemText.Length + ChatAgent.FirstMessage(p.BriefJson, scene: null).Length + (options.MaxOutputTokens ?? 0)));
        Console.Error.WriteLine(
            $"[lab] {planned.Count} characters ({variants.Count} variants x {run.Repeat} {(run.Repeat == 1 ? "take" : "takes")}), written by {run.Model}.");
        // Invariant, because the app speaks English: a Swedish machine would print "200 000".
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"[lab] About {estimate:N0} tokens, against the session cap of {SpendingCap.TokensPerSession:N0}. ") +
            "The cap stops the lab if it runs over.");

        if (estimate > SpendingCap.TokensPerSession)
        {
            Console.Error.WriteLine("[lab] not run: that is over the session cap, so no model call is made. Use fewer variants or a smaller --repeat.");
            RemoveIfMade();
            return null;
        }

        Console.Error.WriteLine("Run this lab? Type yes to run it. Anything else stops before any model call.");
        if (input.ReadLine()?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) != true)
        {
            Console.Error.WriteLine("[lab] not run: the GM did not say yes");
            RemoveIfMade();
            return null;
        }

        // Blind: the run order is shuffled as well as the letters, from one Random, so the order
        // the characters were written in cannot be read as the order of the key.
        var random = new Random(run.Seed!.Value);
        var order = blind ? LabSheet.Shuffle(planned, random) : planned;

        var entries = new List<LabEntry>();
        var capReached = false;

        // The brief on the sheet. On the tone axis the tone is what differs, so it is left off: a
        // blind sheet that showed it would name every character's tone, and the labels already do.
        var shownBrief = plan.Axis == "tone" ? BriefJson.Without(briefJson, "tone") : briefJson;

        // While the variants run, the trace goes nowhere in blind mode: a [skill] line names a
        // path, a [loop] line counts tokens, and either one is the key. Restored in a finally so a
        // crash does not leave the console silent.
        var shown = Console.Error;
        if (blind)
            Console.SetError(TextWriter.Null);
        try
        {
            foreach (var item in order)
            {
                // A copy per variant, so a temperature or a tool list never leaks into the next.
                var variantOptions = options.Clone();
                if (item.Variant.Temperature is { } temperature)
                    variantOptions.Temperature = temperature;

                var writer = new WriterInput(item.BriefJson, null, item.Variant.SkillDir ?? WriterInput.DefaultSkillDir);
                try
                {
                    var text = await ChatAgent.WriteAsync(client, variantOptions, tools, writer, ct);
                    entries.Add(new LabEntry(item.Variant.Label, item.Take, item.SkillLine, text, null));
                }
                catch (SpendingCapReachedException)
                {
                    // Every later call would be refused before it was sent.
                    capReached = true;
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A temperature 400, say: kept with its reason, and the lab goes on.
                    entries.Add(new LabEntry(item.Variant.Label, item.Take, item.SkillLine, null, ex.Message));
                }
            }
        }
        finally
        {
            if (blind)
                Console.SetError(shown);
        }

        if (blind)
        {
            var (sheet, key, data) = LabSheet.Blind(shownBrief, entries, planned.Count, random);
            await File.WriteAllTextAsync(Path.Combine(folder, "blind.md"), sheet, ct);
            await File.WriteAllTextAsync(Path.Combine(folder, "blind.json"), data, ct);
            await File.WriteAllTextAsync(Path.Combine(folder, "key.json"), key, ct);

            // After the window, and a count only: a label or a reason would give away which letters
            // are missing. The reasons are in key.json, which the GM reads after guessing.
            var failed = entries.Count(e => e.Failure is not null);
            if (failed > 0)
                Console.Error.WriteLine($"[lab] {LabSheet.FailedNote(failed)}");
        }
        else
        {
            // Back in the order of the plan, for the sheet.
            var inPlanOrder = entries
                .OrderBy(e => planned.FindIndex(p => p.Variant.Label == e.Label && p.Take == e.Take))
                .ToList();
            await File.WriteAllTextAsync(
                Path.Combine(folder, "sheet.md"), LabSheet.SideBySide(plan.Axis, shownBrief, inPlanOrder, planned.Count), ct);
        }

        if (capReached)
            Console.Error.WriteLine($"[lab] stopped at the spending cap after {entries.Count} of {planned.Count}");
        Console.Error.WriteLine($"[lab] written to {folder}");
        return folder;
    }
}
