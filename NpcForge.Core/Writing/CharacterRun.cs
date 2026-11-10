using Microsoft.Extensions.AI;

namespace NpcForge;

// What stays across a roll again: the arguments the roll is made with (from the flags or from the
// GM's scene) and the scene the writer is sent. Scene is null for a run with answer flags, which has
// none. Provider only names the vendor on the dice line; the model comes from the options (ADR-013, decision 3).
public sealed record Ask(Dictionary<string, object?> RollArguments, string? Scene, string Provider);

// One roll's result: the brief as the dice settled it, typed, and the name and text written from it. A
// save is made from the typed brief, so it cannot carry a brief missing a field (ADR-014, decision 3).
public sealed record Draft(CharacterBrief Brief, Written Written);

// Roll, name and write as one step a run can repeat (ADR-013, decision 3), and the save of what it wrote.
// A front end keeps the order of events; this is the middle of it. Every front end calls the same two
// methods, so the roll, the writer's tools and the save are decided here once.
public static class CharacterRun
{
    // The writer's one tool, and the only list the writer is ever offered. Roll, save, load and list are
    // plain methods, never a tool in this list, so the model cannot see them, call them or skip the roll
    // (ADR-014, decision 5). The lab writes through the same list.
    public static IToolSource WriterTools { get; } = new ConsoleTool(Archetypes.Lookup);

    // Roll: the dice settle the brief before the writer's first turn, from the arguments the run was
    // given. A trait the GM described goes in through its own parameter, and only the rest is rolled. A
    // field the roll cannot read stops the run here with RollRefusedException, before the writer is
    // called. Then the namer names the character inside its dice, and the writer writes it.
    // NoNameException is not caught here: no name followed the dice, so the writer was never called.
    public static async Task<Draft> RollAndWriteAsync(
        IChatClient client, ChatOptions options, Dice dice, Ask ask, CancellationToken ct)
    {
        var brief = dice.Roll(ask.RollArguments);
        var briefJson = BriefJson.Of(brief);

        // The dice, made visible. Every trait in the writing should trace back to a value on this line,
        // and any that does not was invented by the model.
        Console.Error.WriteLine($"[app] {ask.Provider} {options.ModelId}");
        Console.Error.WriteLine($"[app] brief {briefJson}");

        // Shows in every run's record that the writer was given the scene. A count, not the text: the GM
        // typed it, and it is on the screen above.
        if (ask.Scene is not null)
            Console.Error.WriteLine($"[app] scene sent ({ask.Scene.Length} chars)");

        var written = await ChatAgent.WriteAsync(
            client, options, WriterTools, new WriterInput(briefJson, ask.Scene, WriterInput.DefaultSkillDir), ct);
        return new Draft(brief, written);
    }

    // Save: the typed brief with the name the namer gave and the text written from it. The store numbers
    // the save and returns the number (ADR-015).
    public static Task<int> SaveAsync(ICharacterStore store, Draft draft, CancellationToken ct) =>
        store.SaveAsync(SavedCharacter.Of(
            draft.Brief,
            // As the namer split it, so the save shows the count the name was checked on.
            new CharacterName(draft.Written.Name.Syllables, draft.Written.Name.Family),
            draft.Written.Text), ct);
}
