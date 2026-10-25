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
