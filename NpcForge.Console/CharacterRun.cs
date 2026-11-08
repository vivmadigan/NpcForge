using Microsoft.Extensions.AI;

namespace NpcForge;

// What stays across a roll again: the arguments the roll is made with (from the flags or from the
// GM's scene) and the scene the writer is sent. Scene is null for a run with answer flags, which has
// none. Provider only names the vendor on the dice line; the model comes from the options (ADR-013, decision 3).
public sealed record Ask(Dictionary<string, object?> RollArguments, string? Scene, string Provider);

// One roll's result: the brief as the server returned it, and the name and text written from it.
public sealed record Draft(string BriefJson, Written Written);

// Roll, name and write as one step the run can repeat (ADR-013, decision 3). Program.cs keeps the order
// of events; this is the middle of it.
public static class CharacterRun
{
    // Roll: the app calls roll_character itself. The model never sees this tool, so it cannot
    // skip the roll. The difficulty goes as text and the server turns it into the enum; a value
    // it does not know stops the run here, before the writer is called. A trait the GM described
    // goes in through the parameter roll_character already has, and only the rest is rolled.
    // Then the namer names the character inside its dice, and the writer writes it. NoNameException
    // is not caught here: no name followed the dice, so the writer was never called.
    public static async Task<Draft> RollAndWriteAsync(
        IChatClient client, ChatOptions options, McpToolSource tools, Ask ask, CancellationToken ct)
    {
        var briefJson = await tools.CallDirectAsync("roll_character", ask.RollArguments, ct);

        // The dice, made visible. Every trait in the writing should trace back to a value on this line,
        // and any that does not was invented by the model.
        Console.Error.WriteLine($"[app] {ask.Provider} {options.ModelId}");
        Console.Error.WriteLine($"[app] brief {briefJson}");

        // Shows in every run's record that the writer was given the scene. A count, not the text: the GM
        // typed it, and it is on the screen above.
        if (ask.Scene is not null)
            Console.Error.WriteLine($"[app] scene sent ({ask.Scene.Length} chars)");

        var written = await ChatAgent.WriteAsync(
            client, options, tools, new WriterInput(briefJson, ask.Scene, WriterInput.DefaultSkillDir), ct);
        return new Draft(briefJson, written);
    }
}
