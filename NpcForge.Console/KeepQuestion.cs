namespace NpcForge;

// The question after a character is shown: save it or leave it (ADR-013, decisions 1 and 2). Saving
// is the GM's choice in every run, so the only way into save_character is the word "save". A typo
// asks again and never throws away a paid character; only "leave" or end of input does.
public static class KeepQuestion
{
    public const string Prompt = "Keep this character? Type save or leave.";

    // The answer is the GM's own typing, not the model's prose, so code may read it (AGENTS.md).
    // Trimmed and in any case; a blank line and a first letter are not answers.
    public static async Task AskAsync(
        McpToolSource tools, Draft draft, string? charactersPath, TextReader input, TextWriter dialogue, CancellationToken ct)
    {
        dialogue.WriteLine(Prompt);
        while (true)
        {
            var answer = await input.ReadLineAsync(ct);
            if (answer is null)
            {
                dialogue.WriteLine("[app] not saved: end of input");
                return;
            }

            switch (answer.Trim().ToLowerInvariant())
            {
                case "save":
                    var savedAs = await CharacterRun.SaveAsync(tools, draft, ct);
                    // Where, when this run chose the file, so a save in a temp file can be found.
                    dialogue.WriteLine(charactersPath is null
                        ? $"[app] saved as {savedAs}"
                        : $"[app] saved as {savedAs} in {charactersPath}");
                    return;
                case "leave":
                    dialogue.WriteLine("[app] not saved");
                    return;
                default:
                    dialogue.WriteLine($"[app] \"{answer}\" is not save or leave.");
                    dialogue.WriteLine(Prompt);
                    break;
            }
        }
    }
}
