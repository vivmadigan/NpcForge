namespace NpcForge;

// Everything the writer is built from, so a normal run and any other way of writing go through
// ChatAgent.WriteAsync with the same shape (ADR-009: one way to write). SkillDir is a folder
// holding SKILL.md, so a copy with an edit can be tried without touching the bundled one.
public sealed record WriterInput(string BriefJson, string? Scene, string SkillDir)
{
    // The bundled skill, read from next to the exe, not from the repo: the csproj copies skills\
    // into the output folder as content.
    public static string DefaultSkillDir { get; } =
        Path.Combine(AppContext.BaseDirectory, "skills", "npc-writer");
}
