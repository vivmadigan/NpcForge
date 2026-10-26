using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Nodes;           // JsonNode

namespace NpcForge.Tests;

// Reads the replay fixture the way the app wrote it. The writer's calls are found by their system
// message, which is the npc-writer skill, and never by their place: a named run puts the namer's
// calls first (ADR-009), so a lookup by place breaks the day the fixture is re-recorded.
public static class RecordedRun
{
    public static readonly string Fixture = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Tests", "Fixtures", "replay.jsonl");

    // Read with the library's own types, as the console wrote them.
    public static List<ChatMessage> Messages(JsonNode call) =>
        call["request"]!["messages"].Deserialize<List<ChatMessage>>(AIJsonUtilities.DefaultOptions)!;

    public static bool IsWriter(JsonNode call) =>
        Messages(call).First(m => m.Role == ChatRole.System).Text.Contains("name: npc-writer");

    // Every call after line 1 (the flags) that is the writer's.
    public static List<JsonNode> WriterCalls(string[] lines) =>
        [.. lines.Skip(1).Select(line => JsonNode.Parse(line)!).Where(IsWriter)];

    // The brief is the JSON at the end of the writer's first message, after a blank line (see
    // ChatAgent.FirstMessage). Found from the end, so a brace in the scene or the name cannot move it.
    public static string BriefJson(JsonNode writerCall)
    {
        var text = Messages(writerCall).First(m => m.Role == ChatRole.User).Text;
        var at = text.LastIndexOf("\n\n{", StringComparison.Ordinal);
        Assert.True(at >= 0, "the writer's first message holds no brief after a blank line");
        return text[(at + 2)..];
    }

    // A temp copy of the fixture with one namer call in front of the writer's, as a re-recorded
    // fixture will have. The fixture itself is never written. The caller deletes the copy.
    public static string CopyWithNamerFirst()
    {
        var lines = File.ReadAllLines(Fixture);
        var writer = lines.Skip(1).Select(line => JsonSerializer.Deserialize<RecordedCall>(line, Recording.Json)!)
            .First(call => call.Request.Messages.First(m => m.Role == ChatRole.System).Text.Contains("name: npc-writer"));
        var namer = new RecordedCall(
            new RecordedRequest(
                [new(ChatRole.System, "---\nname: namer\n---\nName one character."), new(ChatRole.User, "Name this character.")],
                writer.Request.Options,
                [new RecordedTool("give_name", "Give the character's name.", JsonDocument.Parse("""{"type":"object"}""").RootElement)]),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "Kessa Vale")));
        var path = Path.Combine(Path.GetTempPath(), $"npcforge-namerfirst-{Guid.NewGuid():N}.jsonl");
        File.WriteAllLines(path, [lines[0], JsonSerializer.Serialize(namer, Recording.Json), .. lines.Skip(1)]);
        return path;
    }
}
