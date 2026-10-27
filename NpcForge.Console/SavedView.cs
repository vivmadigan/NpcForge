using System.Text.Json;

namespace NpcForge;

// What --load shows about a saved entry beyond the brief line (ADR-009). An old save is never
// re-typed, so a field added to the brief later is simply absent from it. "Unknown" exists only
// here, on the screen, and never in the file.
public static class SavedView
{
    // The roll schema's fields the saved brief lacks, in schema order, then `name` when the entry has
    // none. Read from the schema, so a field added to the brief later shows as unknown with no edit
    // here.
    public static IReadOnlyList<string> Unknown(IReadOnlyList<RollField> fields, JsonElement savedEntry)
    {
        var brief = savedEntry.GetProperty("brief");
        var missing = fields.Where(field => !brief.TryGetProperty(field.Name, out _)).Select(field => field.Name);
        return [.. NameOf(savedEntry) is null ? missing.Append("name") : missing];
    }

    // The saved name, or null for a save from before the namer.
    public static NamedCharacter? NameOf(JsonElement savedEntry) =>
        savedEntry.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.Object
            ? name.Deserialize<NamedCharacter>(JsonSerializerOptions.Web)
            : null;
}
