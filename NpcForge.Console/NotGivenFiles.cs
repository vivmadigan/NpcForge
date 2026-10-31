namespace NpcForge;

// The not-given files beside a skill (ADR-010, decision 5). A field the scene left out comes back
// from the server as null, and the writer is then told how to write around the gap. The server owns
// which fields may be null (the schema's WhenMissing.NotGiven), so the console keeps no list of them
// and no line here names a field (ADR-007). Same shape as ToneFiles: one path, one check.
public static class NotGivenFiles
{
    // Where a skill folder keeps the guidance for one field. The one place the path is built, so the
    // check below and the system message cannot disagree about it.
    public static string PathOf(string skillDir, string field) => Path.Combine(skillDir, "not-given", $"{field}.md");

    // Every file the schema's NotGiven fields call for and the folders lack, as paths.
    public static IReadOnlyList<string> Missing(IReadOnlyList<RollField> fields, IEnumerable<string> skillDirs)
    {
        var notGiven = fields.Where(field => field.WhenMissing == WhenMissing.NotGiven).Select(field => field.Name).ToList();

        return [.. skillDirs.Distinct()
            .SelectMany(dir => notGiven.Select(field => PathOf(dir, field)))
            .Where(path => !File.Exists(path))];
    }

    // The stop Program.cs makes on the interview path, before anything is paid for. A flag run and
    // a lab never have a null, so they are never checked.
    public static void Require(IReadOnlyList<RollField> fields, IEnumerable<string> skillDirs)
    {
        var missing = Missing(fields, skillDirs);
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Not-given file missing, so nothing was run: {string.Join(", ", missing)}. " +
                "Every field the scene may leave out needs a file in each skill folder's not-given folder.");
    }
}
