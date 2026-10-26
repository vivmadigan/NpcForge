using Microsoft.Extensions.AI;
using System.Security.Cryptography;     // SHA256
using System.Text;                      // Encoding
using System.Text.Json;                 // JsonDocument

namespace NpcForge;

// Build the loop with the tool source it is given, run it, return the answer. It never
// knows whether the tools are fake or MCP; that is IToolSource's job. The brief arrives
// already rolled: rolling is Program.cs's business, not the loop's.

public static class ChatAgent
{
    // The one way to write (ADR-009). Returns the answer; Program.cs prints it, the same way for a
    // run and for a load. Scene on the input is the GM's scene from the interview, or null for a
    // run with answer flags.
    public static async Task<string> WriteAsync(
        IChatClient client, ChatOptions options, IToolSource tools, WriterInput input, CancellationToken ct)
    {
        var loop = new AgentLoop(client, tools, options);

        var system = SystemMessage(input.SkillDir, input.BriefJson);
        Console.Error.WriteLine(system.Trace);

        // The caller owns the history. Guidance at index 0, facts at index 1, and neither is
        // ever repeated. AgentLoop only appends, so the system message stays where it is put.
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, system.Text),
            new(ChatRole.User, FirstMessage(input.BriefJson, input.Scene)),
        };

        return await loop.RunAsync(history, ct);
    }

    // What the model is told, and the line that proves which files that was: the skill, a blank
    // line, then the one tone file the brief names (ADR-009). The tone is read from the brief's JSON,
    // which is structured data. The other two tones are never read into the message, so the model
    // cannot blend them.
    public static (string Text, string Trace) SystemMessage(string skillDir, string briefJson)
    {
        // Read from next to the exe, not from the repo, for the default folder (see
        // WriterInput.DefaultSkillDir). Nothing here parses the files. Whatever they say is what
        // the model is told, which is the whole point of them being files and not strings.
        var skillPath = Path.Combine(skillDir, "SKILL.md");
        var tone = ToneOf(briefJson);
        var text = File.ReadAllText(skillPath).TrimEnd() + "\n\n" + File.ReadAllText(ToneFiles.PathOf(skillDir, tone));

        // Proves which files the model was actually given, and that they came from bin rather
        // than the repo. If editing SKILL.md changes nothing, this line is where you look.
        // The count is the readable half: 2097 to 2239 says it grew by 142. The hash is the
        // reliable half, because two same-length edits have the same count and different
        // hashes, and telling skill versions apart is the whole premise of docs/runs. Hash
        // what the model is sent, not the files on disk, so a BOM or line-ending change on
        // its own does not read as a different skill.
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8].ToLowerInvariant();

        return (text, $"[skill] {skillPath} + tones/{tone}.md ({text.Length} chars, {fingerprint})");
    }

    private static string ToneOf(string briefJson)
    {
        using var brief = JsonDocument.Parse(briefJson);
        return brief.RootElement.TryGetProperty("tone", out var tone) && tone.GetString() is { Length: > 0 } name
            ? name
            : throw new InvalidOperationException("The brief names no tone, so no tone file can be chosen for the writer.");
    }

    // The writer's facts (ADR-008). With no scene, today's message to the byte, so the replay
    // fixture, a flag run, still matches. The scene goes in as typed and code never reads it. The
    // brief stays last, so a replay of a scene run can find it from the end (the scene may hold a
    // brace), and it wins on a clash, so the dice keep their job. How to use the scene is the skill's business, not this message's.
    public static string FirstMessage(string briefJson, string? scene) =>
        scene is null
            ? $"Write this character. The brief is settled; do not change it.\n\n{briefJson}"
            : "Write this character. The brief is settled; do not change it.\n\n" +
              "This is the scene the GM described, in their own words. Where it and the brief disagree, the brief wins.\n\n" +
              $"<scene>\n{scene}\n</scene>\n\n{briefJson}";
}
