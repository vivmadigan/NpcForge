using System.Text.Json;

namespace NpcForge;

// What --list prints (ADR-013, decision 5), from the rows list_characters sent. The console has no
// type for the brief: it gets the two fields the server picked, under the brief's own names, and
// the name in the shape a save holds, so NamedCharacter.Full stays the one place that joins the
// syllables.
public static class SaveList
{
    private sealed record Row(int Number, NamedCharacter? Name, string? Occupation, string? Setting);

    // One line per save: number, name, occupation, setting, each column padded to its widest entry
    // and separated by two spaces. A save with no name shows (no name), as an old save does, and a
    // brief without an occupation or a setting shows a dash, so a column never closes up.
    public static IReadOnlyList<string> Rows(string listJson)
    {
        var rows = JsonSerializer.Deserialize<List<Row>>(listJson, JsonSerializerOptions.Web) ?? [];
        var cells = rows.Select(row => new[]
        {
            row.Number.ToString(),
            row.Name?.Full ?? "(no name)",
            row.Occupation ?? "-",
            row.Setting ?? "-",
        }).ToList();

        // Empty input has no widest entry, and nothing to pad.
        if (cells.Count == 0)
            return [];

        var widths = Enumerable.Range(0, 3).Select(column => cells.Max(c => c[column].Length)).ToList();
        return [.. cells.Select(c => string.Join("  ",
            c[0].PadLeft(widths[0]), c[1].PadRight(widths[1]), c[2].PadRight(widths[2]), c[3]))];
    }
}
