using System.Text.Json;

namespace NpcForge;

// What --load shows about a saved character beyond the brief line (ADR-009). An old save is never
// re-typed, so a field added to the brief later is simply absent from it. "Unknown" exists only
// here, on the screen, and never in a store.
public static class SavedView
{
    // The roll schema's fields the saved brief lacks, in schema order, then `name` when the save has
    // none. Read from the schema, so a field added to the brief later shows as unknown with no edit
    // here.
    public static IReadOnlyList<string> Unknown(IReadOnlyList<RollField> fields, SavedCharacter saved)
    {
        var missing = fields.Where(field => !saved.Brief.TryGetProperty(field.Name, out _)).Select(field => field.Name);
        return [.. saved.Name is null ? missing.Append("name") : missing];
    }

    // The schema's fields the saved brief holds as a JSON null, in schema order: a scene that left a
    // NotGiven field out (ADR-010). A missing key is Unknown instead, so no field is in both lists.
    public static IReadOnlyList<string> NotGiven(IReadOnlyList<RollField> fields, SavedCharacter saved) =>
        [.. fields
            .Where(field => saved.Brief.TryGetProperty(field.Name, out var value) && value.ValueKind == JsonValueKind.Null)
            .Select(field => field.Name)];

    // The saved name, or null for a save from before the namer. As a NamedCharacter, so Full stays the
    // one place that joins the syllables.
    public static NamedCharacter? NameOf(SavedCharacter saved) =>
        saved.Name is { } name ? new NamedCharacter(name.Syllables, name.Family) : null;
}
