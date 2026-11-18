namespace NpcForge.Api;

// What the saved page reads, one type for each route. The field names are the ones in
// NpcForge.Web/src/api.ts, in camelCase on the wire. These are the API's own types and no core type
// is serialised, so a rename in the core cannot rename a field the page reads (ADR-019, decision 3).
// Not named ListedCharacter or SavedCharacter: the core has both, in the namespace above this one.

// One row of the list. Never the text.
internal sealed record SaveRow(int Number, string? Name, string? Occupation, string? Setting);

// One save, opened. The sheet is the saved text as it was written.
internal sealed record SaveSheet(int Number, string? Name, string Sheet);
