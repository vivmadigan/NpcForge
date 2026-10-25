using System.Text.Json;

namespace NpcForge;

// One roll_character parameter, read from the server's tools/list. The server is the one owner of
// the brief's fields, so the console keeps no copy of their names, order, choices or which ones
// are required (ADR-007; two processes, no shared contracts project). A trait added on the server
// reaches the interview, the confirmation and the roll with no console change.
//
// Choices: the fixed list for a choice field (difficulty, obstacle), null for free text. The kind
// decides what the interview may record: a choice may be inferred, free text must be the GM's words.
// Required: in the schema's "required" list. Never rolled: an unmentioned one takes its flag default.
public sealed record RollField(string Name, string Description, IReadOnlyList<string>? Choices, bool Required)
{
    // The boundary: a JSON schema in, the field list out. A property shape this does not know
    // throws, so a server change fails a free test instead of a paid run.
    public static IReadOnlyList<RollField> ListFrom(JsonElement inputSchema)
    {
        var required = inputSchema.TryGetProperty("required", out var names)
            ? names.EnumerateArray().Select(name => name.GetString()!).ToHashSet()
            : [];

        var fields = new List<RollField>();
        foreach (var property in inputSchema.GetProperty("properties").EnumerateObject())
        {
            var schema = property.Value;

            var description = schema.TryGetProperty("description", out var text) ? text.GetString() : null;
            if (string.IsNullOrWhiteSpace(description))
                throw new InvalidOperationException(
                    $"roll_character's '{property.Name}' has no description, and the interviewer reads it to know the field.");

            if (!IsText(schema))
                throw new InvalidOperationException(
                    $"roll_character's '{property.Name}' is not text ({schema.GetRawText()}), and the interview records only text.");

            // The SDK writes a nullable enum as type ["string","null"] with a null among the choices.
            // The null means "not handed in", which is not a choice the GM can make.
            IReadOnlyList<string>? choices = schema.TryGetProperty("enum", out var list)
                ? [.. list.EnumerateArray().Where(choice => choice.ValueKind == JsonValueKind.String).Select(choice => choice.GetString()!)]
                : null;

            fields.Add(new RollField(property.Name, description, choices, required.Contains(property.Name)));
        }

        return fields;
    }

    // "string", or ["string","null"] for a parameter that may be left out.
    private static bool IsText(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
            return false;

        return type.ValueKind == JsonValueKind.String
            ? type.GetString() == "string"
            : type.ValueKind == JsonValueKind.Array
              && type.EnumerateArray().Any(t => t.GetString() == "string")
              && type.EnumerateArray().All(t => t.GetString() is "string" or "null");
    }
}
