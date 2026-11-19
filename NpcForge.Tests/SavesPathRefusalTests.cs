namespace NpcForge.Tests;

// A saves path read from the current drive's root is refused before the run, through the whole app as
// a process: before the fix the console saved a /c/Users/... path under C:\c\Users\... and then said it
// had saved in /c/Users/.... RunOptionsTests holds each form's refusal in process. Free: a replay with
// the offline stop and the dead proxy.
[Trait("Category", "Process")]
public class SavesPathRefusalTests : IDisposable
{
    // The temp file with its drive cut off: a broken guard saves it on the current drive, which is the
    // temp folder's own on this machine, so a failing run leaves its file in temp and nowhere else.
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"npcforge-drive-rooted-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_file);

    [Fact]
    public async Task A_saves_path_from_the_current_drive_stops_the_run_and_saves_nothing()
    {
        var driveRooted = _file[Path.GetPathRoot(_file)!.Length..].Replace('\\', '/').Insert(0, "/");
        var flags = System.Text.Json.JsonSerializer.Deserialize<string[]>(File.ReadLines(RecordedRun.Fixture).First())!;

        var run = await ConsoleProcess.RunAsync(
            ["--provider", "replay", "--recording", RecordedRun.Fixture, .. flags],
            offline: true, environment: new() { ["NPCFORGE_CHARACTERS"] = driveRooted }, input: "save\n");

        Assert.NotEqual(0, run.ExitCode);
        Assert.Contains($"NPCFORGE_CHARACTERS is \"{driveRooted}\"", run.Stderr);
        Assert.DoesNotContain("[app] saved as", run.Stderr);
        Assert.False(File.Exists(_file));
    }
}
