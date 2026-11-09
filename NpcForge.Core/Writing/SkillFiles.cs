namespace NpcForge;

// The core's one door to the disk: the bundled skills and the lab's skill folders are product text read
// from a folder, never a save and never a front end's file, so they do not go behind a port (ADR-014,
// decision 6). Every other file read in the core is a compile error (BannedSymbols.txt), and this is the one
// file that may switch that rule off, which CoreReferenceTests holds. Two calls and nothing more: widening
// this door needs an ADR.
public static class SkillFiles
{
#pragma warning disable RS0030 // The one allowed file read, for skills only (ADR-014, decision 6).
    public static string Read(string path) => File.ReadAllText(path);

    public static bool Exists(string path) => File.Exists(path);
#pragma warning restore RS0030
}
