namespace NpcForge;

// What the GM chose for the character on the screen. EndOfInput is a leave the GM did not type: the
// input ran out, so it keeps nothing, and the run says why.
public enum Keep { Save, Again, Leave, EndOfInput }

// The question after a character is shown: save it, roll again for the same scene, or leave it
// (ADR-013, decisions 1 to 3). It only asks; Program.cs acts on the answer, so the order of events
// (roll, show, ask, save or roll again) stays in one place. A typo asks again and never throws away
// a paid character; only "leave" or end of input does.
public static class KeepQuestion
{
    public const string Prompt = "Keep this character? Type save, again (new dice, same scene) or leave.";

    // The answer is the GM's own typing, not the model's prose, so code may read it (AGENTS.md).
    // Trimmed and in any case; a blank line and a first letter are not answers, because a single
    // letter is one key away from another answer.
    public static async Task<Keep> AskAsync(TextReader input, TextWriter dialogue, CancellationToken ct)
    {
        dialogue.WriteLine(Prompt);
        while (true)
        {
            var answer = await input.ReadLineAsync(ct);
            if (answer is null)
                return Keep.EndOfInput;

            // A byte order mark is not whitespace, so Trim leaves it. A pipe that writes one puts it
            // before the first answer, and "again" would then be refused and the next "save" would save
            // the wrong character.
            switch (answer.Trim().TrimStart('﻿').ToLowerInvariant())
            {
                case "save":
                    return Keep.Save;
                case "again":
                    return Keep.Again;
                case "leave":
                    return Keep.Leave;
                default:
                    dialogue.WriteLine($"[app] \"{answer}\" is not save, again or leave.");
                    dialogue.WriteLine(Prompt);
                    break;
            }
        }
    }
}
