using Microsoft.Extensions.AI;
using System.Security.Cryptography;     // SHA256
using System.Text;                      // Encoding

namespace NpcForge;

// Build the loop with the tool source it is given, run it, print the answer. It never
// knows whether the tools are fake or MCP; that is IToolSource's job. The brief arrives
// already rolled: rolling is Program.cs's business, not the loop's.

public static class ChatAgent
{
    public static async Task RunAsync(IChatClient client, ChatOptions options, IToolSource tools, string briefJson)
    {
        var loop = new AgentLoop(client, tools, options);

        // Read from next to the exe, not from the repo: the csproj copies skills\ into the
        // output folder as content. Nothing here parses the file. Whatever it says is what
        // the model is told, which is the whole point of it being a file and not a string.
        var skillPath = Path.Combine(AppContext.BaseDirectory, "skills", "npc-writer", "SKILL.md");
        var skill = File.ReadAllText(skillPath);

        // Proves which file the model was actually given, and that it came from bin rather
        // than the repo. If editing SKILL.md changes nothing, this line is where you look.
        // The count is the readable half: 2097 to 2239 says it grew by 142. The hash is the
        // reliable half, because two same-length edits have the same count and different
        // hashes, and telling skill versions apart is the whole premise of docs/runs. Hash
        // what the model is sent, not the file on disk, so a BOM or line-ending change on
        // its own does not read as a different skill.
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(skill)))[..8].ToLowerInvariant();

        Console.Error.WriteLine($"[skill] {skillPath} ({skill.Length} chars, {fingerprint})");

        // The caller owns the history. Guidance at index 0, facts at index 1, and neither is
        // ever repeated. AgentLoop only appends, so the system message stays where it is put.
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, skill),
            new(ChatRole.User, $"Write this character. The brief is settled; do not change it.\n\n{briefJson}"),
        };

        var answer = await loop.RunAsync(history, CancellationToken.None);
        Console.WriteLine(answer);

    }
}
