namespace NpcForge;

// Everything the writer is built from, so a normal run and any other way of writing go through
// ChatAgent.WriteAsync with the same shape (ADR-009: one way to write). SkillDir is a folder
// holding SKILL.md, so a copy with an edit can be tried without touching the bundled one. Name is
// a name the namer already gave, so a lab writes every variant with one name; null runs the namer.
public sealed record WriterInput(string BriefJson, string? Scene, string SkillDir, NamedCharacter? Name = null)
{
    // The bundled skill, read from next to the exe, not from the repo: the csproj copies skills\
    // into the output folder as content.
    public static string DefaultSkillDir { get; } =
        Path.Combine(AppContext.BaseDirectory, "skills", "npc-writer");
}

// What the writer hands back: the name it was told and the text it wrote, so the save keeps both.
public sealed record Written(NamedCharacter Name, string Text);
