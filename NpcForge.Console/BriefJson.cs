using System.Text.Json;
using System.Text.Json.Nodes;           // JsonNode

namespace NpcForge;

// The brief is the server's JSON and the console has no type for it, so the few things the console
// does to one are done on the JSON itself, here.
public static class BriefJson
{
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
