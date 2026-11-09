using System.Text.Json;
using System.Text.Json.Nodes;           // JsonNode

namespace NpcForge;

// The brief as JSON text, and the few things done to a brief on its JSON itself. The text is what the
// writer's first message carries, and the console still reads it from the server until the switch.
public static class BriefJson
{
    // The brief's JSON options, decided once. BriefJson.Of writes the text the writer is sent and
    // SavedCharacter.Of keeps the brief a save holds, so the two cannot drift apart (ADR-015, decision 5).
    // The names are camelCase and the enums write their own names (CharacterBrief). The encoder is the
    // default one, which writes an apostrophe, a markup character or a letter such as Æ as a backslash-u
    // escape: that is how the MCP SDK writes the text the writer is sent today, and the writer must keep
    // reading the same bytes after the switch. A save written to the file is re-encoded by the file's own
    // writer, so its bytes do not depend on this.
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    // The brief as the roll_character tool returned it, byte for byte (CoreMatchesServerTests).
    public static string Of(CharacterBrief brief) => JsonSerializer.Serialize(brief, Options);

    // roll_character's arguments for the same brief with one field changed: every field of the brief
    // handed back in, `field` set to `value`. The server stays the only author of briefs, and since
    // every field is given, every die it still casts is replaced by the value given (ADR-009).
    public static Dictionary<string, object?> WithField(string briefJson, string field, string value)
    {
        using var brief = JsonDocument.Parse(briefJson);
        var arguments = brief.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        arguments[field] = value;
        return arguments;
    }

    // The brief as it is shown on a lab's sheet when one field is what the sheet compares: a blind
    // sheet that showed the tone would name every character's tone.
    public static string Without(string briefJson, string field)
    {
        var brief = JsonNode.Parse(briefJson)!.AsObject();
        brief.Remove(field);
        return brief.ToJsonString();
    }
}
