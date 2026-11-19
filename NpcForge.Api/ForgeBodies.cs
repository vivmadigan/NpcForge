namespace NpcForge.Api;

// What the forge's four routes read and write, one type for each body. The field names and kinds are
// the ones in NpcForge.Web/src/api.ts, in camelCase on the wire. As with the saves, these are the API's
// own types and no core type is serialised, so a rename in the core cannot rename a field the page
// reads (ADR-019, decision 3). An outcome is a 200 body with a `kind`; a fault is a problem and has
// none (decision 4).

// The two request bodies. A body with the field missing arrives as null and is refused as blank.
internal sealed record SceneBody(string? Scene);

internal sealed record AnswerBody(string? Text);

// What a step of the interview answers: the next question, or the confirmation.
internal sealed record QuestionStep(string Interview, string Field, string Question)
{
    public string Kind => "question";
}

// One field the scene settled, with the value the brief will hold, or null for "not given" (ADR-010).
// A field the dice will pick has no entry.
internal sealed record ConfirmEntry(string Field, string? Value);

internal sealed record ConfirmStep(string Interview, IReadOnlyList<ConfirmEntry> Entries)
{
    public string Kind => "confirm";
}

// The spending cap, at any model call. Nothing was saved.
internal sealed record StoppedStep(string Reason)
{
    public string Kind => "stopped";
}

// A written character. `Draft` is the id a save is made from. The name arrives joined and the brief
// does not cross (ADR-018, decision 9).
internal sealed record WrittenRun(string Draft, string Name, string Sheet)
{
    public string Kind => "written";
}

// No name followed the dice, so the writer was never called.
internal sealed record NoNameRun(string Reason)
{
    public string Kind => "no-name";
}

internal sealed record SavedAs(int Number);
