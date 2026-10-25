using Microsoft.Extensions.AI;
using System.Globalization;             // CultureInfo, NumberStyles

namespace NpcForge;

// One thing a lab changes. A skill variant changes the writer's skill folder; a temperature variant
// changes ChatOptions.Temperature. Exactly one of the two is set, and nothing else differs between
// variants (ADR-009).
public sealed record LabVariant(string Label, string? SkillDir, float? Temperature);

// What --lab says: one axis and the values to compare on it. Parsed in one place so the flag parser
// can refuse a bad one before any model client is built, and the lab reads the same plan after.
public sealed record LabPlan(string Axis, IReadOnlyList<LabVariant> Variants)
{
    // The axes that exist. Tone arrives with tone (ADR-009 build order, PR 7).
    private static readonly string[] Axes = ["skill", "temperature"];

    public static LabPlan Parse(string text)
    {
        var at = text.IndexOf('=');
        var axis = (at < 0 ? text : text[..at]).Trim().ToLowerInvariant();

        // Said apart from "unknown", because it is a real axis the GM read about in the ADR.
        if (axis == "tone")
            throw new ArgumentException(
                "--lab tone is not built yet: the tone axis arrives with tone itself (ADR-009, PR 7). " +
                $"The axes that exist now are: {string.Join(", ", Axes)}.");

        if (!Axes.Contains(axis))
            throw new ArgumentException($"Unknown lab axis '{axis}'. The axes are: {string.Join(", ", Axes)}.");

        // Folder paths are read as typed, so a comma cannot be part of one. A blank entry is dropped,
        // so a trailing comma does not make a variant out of nothing.
        var values = at < 0
            ? []
            : text[(at + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // One difference needs two things to differ.
        if (values.Length < 2)
            throw new ArgumentException(
                $"--lab {axis} needs at least two values to compare, for example --lab {Example(axis)}.");

        return axis == "skill" ? SkillVariants(values) : TemperatureVariants(values);
    }

    private static string Example(string axis) =>
        axis == "skill" ? "skill=skills/a,skills/b" : "temperature=0.2,0.9";

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
        // the same name in different places fall back to the whole path.
        var names = full.Select(Path.GetFileName).ToList();
        var variants = full.Select((path, i) => new LabVariant(
            names.Count(name => name == names[i]) > 1 ? path : names[i]!, path, null));
        return new LabPlan("skill", [.. variants]);
    }

    private static LabPlan TemperatureVariants(string[] values)
    {
        var variants = values.Select(value =>
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t)
                || !float.IsFinite(t) || t < 0)
                throw new ArgumentException($"--lab temperature needs numbers of 0 or more, and '{value}' is not one.");
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
    private sealed record Planned(LabVariant Variant, int Take, string SystemText, string SkillLine);

    // --score: reads a finished blind lab, asks for a guess per letter, and writes scored.md. It
    // needs no model, no key and no server, so Program.cs runs it before any of them exist. The
    // dialogue goes to `dialogue` (stderr) and the result to `result` (stdout), like a run.
    public static async Task ScoreAsync(
        string folder, TextReader input, TextWriter dialogue, TextWriter result, CancellationToken ct)
    {
        var blindPath = Path.Combine(folder, "blind.md");
        var keyPath = Path.Combine(folder, "key.json");
        if (!File.Exists(blindPath) || !File.Exists(keyPath))
            throw new InvalidOperationException(
                $"--score needs a blind lab: {folder} must hold blind.md and key.json (made by --lab with --mode blind).");

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

    // Returns the folder the sheet is in, or null when the lab stopped before any model call.
    public static async Task<string?> RunAsync(
        IChatClient client, ChatOptions options, McpToolSource tools, RunOptions run, TextReader input, CancellationToken ct)
    {
        var plan = run.Lab ?? throw new ArgumentException("Lab.RunAsync needs a --lab run.");
        var blind = run.Mode == "blind";
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
        if (new[] { "sheet.md", "blind.md", "key.json", "scored.md" }.Any(name => File.Exists(Path.Combine(folder, name))))
        {
            Console.Error.WriteLine($"[lab] not run: {folder} already holds a lab. Name a new --out folder.");
            return null;                // an old lab's folder is not ours to remove
        }

        void RemoveIfMade()
        {
            if (madeFolder)
                Directory.Delete(folder);
        }

        // Every skill is read now, so a missing folder stops the lab here and not after the first
        // paid call. Temperature variants all use the bundled skill.
        var planned = plan.Variants
            .SelectMany(variant => Enumerable.Range(1, run.Repeat).Select(take =>
            {
                var system = ChatAgent.SystemMessage(variant.SkillDir ?? WriterInput.DefaultSkillDir);
                return new Planned(variant, take, system.Text, system.Trace);
            }))
            .ToList();

        // The roll is free, and the estimate needs the first message it makes. The seed is the
        // server's, so every variant is written from this one brief.
        var briefJson = await tools.CallDirectAsync("roll_character", run.RollArguments(), ct);
        var firstMessage = ChatAgent.FirstMessage(briefJson, scene: null);

        // It asks first (costly-steps playbook). Each character is two calls at most, each costing
        // its whole input plus the most the model may answer. Characters stand in for tokens, so
        // this is an upper bound and says "about". Tokens only, no prices.
        var estimate = planned.Sum(p => 2L * (p.SystemText.Length + firstMessage.Length + (options.MaxOutputTokens ?? 0)));
        Console.Error.WriteLine(
            $"[lab] {planned.Count} characters ({plan.Variants.Count} variants x {run.Repeat} takes), written by {run.Model}.");
        Console.Error.WriteLine(
            $"[lab] About {estimate:N0} tokens at most, against the session cap of {SpendingCap.TokensPerSession:N0}.");

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

                var writer = new WriterInput(briefJson, null, item.Variant.SkillDir ?? WriterInput.DefaultSkillDir);
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
            var (sheet, key) = LabSheet.Blind(briefJson, entries, planned.Count, random);
            await File.WriteAllTextAsync(Path.Combine(folder, "blind.md"), sheet, ct);
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
                Path.Combine(folder, "sheet.md"), LabSheet.SideBySide(plan.Axis, briefJson, inPlanOrder, planned.Count), ct);
        }

        if (capReached)
            Console.Error.WriteLine($"[lab] stopped at the spending cap after {entries.Count} of {planned.Count}");
        Console.Error.WriteLine($"[lab] written to {folder}");
        return folder;
    }
}
