using Microsoft.Extensions.AI;

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
        Console.Error.WriteLine($"[skill] {skillPath} ({skill.Length} chars)");


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
