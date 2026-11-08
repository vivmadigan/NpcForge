using System.Text.Json;
using System.Text.Json.Serialization;  // JsonIgnore

namespace NpcForge;

// One saved run. The model writes differently each time, so the name the namer gave and the
// text are saved beside the brief. The brief stays the JSON it was saved as: an old entry is never passed
// through CharacterBrief, so a field added to the brief later cannot appear in it (ADR-009).
// An old save has no name. Null is left out of the file, so an old entry keeps its bytes when
// the file is written back.
public sealed record SavedCharacter(
    JsonElement Brief,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CharacterName? Name,
    string Text)
{
    // The same camelCase names as the [app] brief line. The enums write their names from their own
    // attributes (CharacterBrief). The encoder and the indentation belong to the file that writes the
    // element, not to the element.
    private static readonly JsonSerializerOptions BriefOptions = new(JsonSerializerDefaults.Web);

    // A new save: the typed brief as the JSON it is kept as. Only a new save goes through
    // CharacterBrief, so every new save has every field and a name (ADR-009).
    public static SavedCharacter Of(CharacterBrief brief, CharacterName name, string text) =>
        new(JsonSerializer.SerializeToElement(brief, BriefOptions), name, text);
}

// The name the namer gave and the console accepted, split as the namer split it, so the count it
// was checked on stays readable in the save.
public sealed record CharacterName(IReadOnlyList<string> Syllables, string Family);

// One row of the list. The name keeps the shape the save holds, so the console reads it as it
// reads a loaded save's. The fields keep the brief's own names (ADR-013).
public sealed record ListedCharacter(int Number, CharacterName? Name, string? Occupation, string? Setting)
{
    // Only the name, the occupation and the setting leave a store: the text is the model's prose and is
    // never read here, and the rest of the brief stays in the save. A brief without one of the two
    // keys gives null for it, so the 16 saves from before the namer list as they are. Every store builds
    // its rows here, so the rule is written once.
    public static ListedCharacter Of(int number, SavedCharacter saved) =>
        new(number, saved.Name, BriefText(saved.Brief, "occupation"), BriefText(saved.Brief, "setting"));

    // The brief is the JSON it was saved as, so a key is read by its saved name, not through
    // CharacterBrief (ADR-009).
    private static string? BriefText(JsonElement brief, string key) =>
        brief.ValueKind == JsonValueKind.Object
        && brief.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
