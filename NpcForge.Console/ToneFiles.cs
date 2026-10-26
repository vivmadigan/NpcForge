namespace NpcForge;

// The tone files beside a skill (ADR-009 section 3). The server owns the list of tones, so the
// console reads it from roll_character's schema and keeps no copy: a tone added on the server is a
// missing file here until someone writes it, and that stops a run before it is paid for.
public static class ToneFiles
{
    // Where a skill folder keeps the file for one tone. The one place the path is built, so the
    // check below and the system message cannot disagree about it.
    public static string PathOf(string skillDir, string tone) => Path.Combine(skillDir, "tones", $"{tone}.md");

    // Every file the schema's tone choices call for and the folders lack, as paths.
    public static IReadOnlyList<string> Missing(IReadOnlyList<RollField> fields, IEnumerable<string> skillDirs)
    {
        var tones = fields.FirstOrDefault(field => field.Name == "tone")?.Choices
            ?? throw new InvalidOperationException(
                "roll_character has no 'tone' choice list, so the tone files cannot be checked.");

        return [.. skillDirs.Distinct()
            .SelectMany(dir => tones.Select(tone => PathOf(dir, tone)))
            .Where(path => !File.Exists(path))];
    }

    // The stop Program.cs and the lab make before anything is paid for.
    public static void Require(IReadOnlyList<RollField> fields, IEnumerable<string> skillDirs)
    {
        var missing = Missing(fields, skillDirs);
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Tone file missing, so nothing was run: {string.Join(", ", missing)}. " +
                "Every tone the server rolls needs a file in each skill folder's tones folder.");
    }
}
