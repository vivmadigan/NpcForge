namespace NpcForge.Tests;

// A saves path that is not a full path is refused before the run, through the whole app as a process:
// before the fix the console saved a /c/Users/... path under C:\c\Users\... and then said it had saved
// in /c/Users/.... RunOptionsTests holds each form's refusal in process. Free: a replay with the offline
// stop and the dead proxy.
[Trait("Category", "Process")]
public class SavesPathRefusalTests : IDisposable
{
    // The temp file with its drive cut off, the shape of a Git Bash path. The child inherits this
    // process's folder, so a broken guard saves at this path's GetFullPath here: that is the file the
    // test checks and deletes, on whichever drive it lands.
    private static readonly string TempFile = Path.Combine(Path.GetTempPath(), $"npcforge-drive-rooted-{Guid.NewGuid():N}.json");

    private static readonly string DriveRooted = "/" + TempFile[Path.GetPathRoot(TempFile)!.Length..].Replace('\\', '/');

    private readonly string _whereABrokenGuardSaves = Path.GetFullPath(DriveRooted);

    public void Dispose() => File.Delete(_whereABrokenGuardSaves);

    [Fact]
    public async Task A_saves_path_from_the_current_drive_stops_the_run_and_saves_nothing()
    {
        var flags = System.Text.Json.JsonSerializer.Deserialize<string[]>(File.ReadLines(RecordedRun.Fixture).First())!;

        var run = await ConsoleProcess.RunAsync(
            ["--provider", "replay", "--recording", RecordedRun.Fixture, .. flags],
            offline: true, environment: new() { ["NPCFORGE_CHARACTERS"] = DriveRooted }, input: "save\n");

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains($"NPCFORGE_CHARACTERS is \"{DriveRooted}\"", run.Stderr);
        Assert.DoesNotContain("[app] saved as", run.Stderr);
        Assert.False(File.Exists(_whereABrokenGuardSaves));
    }
}
