using System.Text.Json;

namespace NpcForge;

// What --load shows about a saved entry beyond the brief line (ADR-009). An old save is never
// re-typed, so a field added to the brief later is simply absent from it. "Unknown" exists only
// here, on the screen, and never in the file.
public static class SavedView
{
    // The roll schema's fields the saved brief lacks, in schema order. Read from the schema, so a
    // field added to the brief later shows as unknown with no edit here.
    public static IReadOnlyList<string> Unknown(IReadOnlyList<RollField> fields, JsonElement savedEntry)
    {
        var brief = savedEntry.GetProperty("brief");
        return [.. fields.Where(field => !brief.TryGetProperty(field.Name, out _)).Select(field => field.Name)];
    }
}
