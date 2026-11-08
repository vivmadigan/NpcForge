using System.Text.Json;

namespace NpcForge.Tests;

// A save file for tests that list saves, in the shape the owner's real save file has:
// number to { brief, name?, text }. That file is only ever read to learn the shape, never
// written (AGENTS.md "My saves"). The keys are not in number order and one save has no name, so a
// list that sorts as text, or that needs a name, fails here.
internal static class SavedFile
{
    // Never printed by a list: it reads the name and the brief, not the model's prose.
    public const string TextMarker = "TEXT-A-LIST-MUST-NOT-READ";

    public static string WriteTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), $"npcforge-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, Contents);
        return path;
    }

    // Save 8 is the old shape: no name. Save 10 has a brief with no occupation.
    private static readonly string Contents = JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["10"] = new
        {
            brief = new { setting = "a river barge", playersWant = "the name of a fence" },
            name = new { syllables = new[] { "Tam" }, family = "Reed" },
            text = TextMarker,
        },
        ["9"] = new
        {
            brief = new { setting = "an inn in a major city", occupation = "innkeeper", tone = "Heartfelt" },
            name = new { syllables = new[] { "Har", "grim" }, family = "Rockhammer" },
            text = TextMarker,
        },
        ["8"] = new
        {
            brief = new { setting = "tiny bakery", occupation = "goblin baker", tone = "Comic" },
            text = TextMarker,
        },
    });
}
