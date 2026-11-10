namespace NpcForge.Tests;

// ADR-015, decision 4: a save file that will not parse is a plain exception from the file store, and the
// console reports it as one [app] line that names the file, with exit code 1 and no stack trace, on
// --list, --load and the save after "save". Before the switch, --list and the save ended in an unhandled
// exception. The file is never written over. Free: the offline stop and the dead proxy, and the replay
// provider for the run that saves.
[Trait("Category", "Process")]
public class UnreadableSaveFileTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"npcforge-unreadable-{Guid.NewGuid():N}.json");

    private const string Broken = "{ not json";

    public UnreadableSaveFileTests() => File.WriteAllText(_file, Broken);

    public void Dispose() => File.Delete(_file);

    [Theory]
    [InlineData("list", "[app] not listed: ")]
    [InlineData("load", "[app] not loaded: ")]
    [InlineData("save", "[app] not saved: ")]
    public async Task An_unreadable_save_file_is_one_line_that_names_it_and_exit_1(string command, string prefix)
    {
        var flags = command switch
        {
            "list" => new[] { "--list" },
            "load" => ["--load", "1"],
            // Line 1 of the fixture is the flags it was recorded with, so the run reaches the question.
            _ => [.. System.Text.Json.JsonSerializer.Deserialize<string[]>(File.ReadLines(RecordedRun.Fixture).First())!,
                  "--provider", "replay", "--recording", RecordedRun.Fixture],
        };

        var run = await ConsoleProcess.RunAsync(
            flags, offline: true, environment: new() { ["NPCFORGE_CHARACTERS"] = _file }, input: "save\n");

        Assert.True(run.ExitCode == 1, $"Exit code {run.ExitCode}.\n{run.Stderr}");
        var lines = run.Stderr.ReplaceLineEndings().Split(Environment.NewLine);
        var reported = Assert.Single(lines, line => line.StartsWith(prefix, StringComparison.Ordinal));
        Assert.StartsWith($"{prefix}Could not read {_file}: ", reported);
        Assert.DoesNotContain("Unhandled exception", run.Stderr);
        Assert.DoesNotContain(lines, line => line.TrimStart().StartsWith("at "));
        Assert.Equal(Broken, File.ReadAllText(_file));
    }
}
