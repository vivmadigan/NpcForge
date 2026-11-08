namespace NpcForge.Tests;

// A copy of the console's own build, for a test that deletes one of its files and runs it. A sibling
// of the console's bin folder, so the copy still finds the server four folders up from itself, as
// McpToolSource looks. bin\ is ignored by git and by the build's staleness check. ToneFileTests and
// NotGivenFileTests both copy through here, so ToneFileTests' one run of an intact copy is the
// positive control for both: their missing-file runs stop at their check and not somewhere earlier.
internal static class ConsoleBuildCopy
{
    // The folder goes on the caller's cleanup list before the copy starts, so a copy that fails half
    // way is still removed.
    public static string Make(string label, List<string> cleanup)
    {
        var bin = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Console", "bin", ConsoleProcess.BuildConfiguration);
        var copy = Path.Combine(bin, $"net10.0-{label}-{Guid.NewGuid():N}");
        cleanup.Add(copy);
        CopyDirectory(Path.Combine(bin, "net10.0"), copy);
        return copy;
    }

    // The copy as an interview run. The replay provider and the dead proxy make it free; no answer
    // flag makes it an interview run, which would print the greeting first if a file check came after it.
    public static async Task<ConsoleProcess.Result> RunAsync(string copy)
    {
        var save = Path.Combine(Path.GetTempPath(), $"npcforge-buildcopy-{Guid.NewGuid():N}.json");
        try
        {
            return await ConsoleProcess.RunAsync(
                ["--provider", "replay", "--recording", RecordedRun.Fixture],
                offline: true,
                environment: new() { ["NPCFORGE_CHARACTERS"] = save },
                consoleDll: Path.Combine(copy, "NpcForge.Console.dll"));
        }
        finally { File.Delete(save); }
    }

    public static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(from))
            CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}
