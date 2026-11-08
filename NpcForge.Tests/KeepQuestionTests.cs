using System.Text.Json;
using System.Text.Json.Nodes;           // JsonObject, JsonNode

namespace NpcForge.Tests;

// ADR-013, decisions 1 and 2: after the character is shown the app asks, and only "save" saves.
// Each test runs the whole app as a process against the recorded run, free, and reads the save file,
// which is exact; the printed lines are checked as text the GM reads (lesson 006). The run is a flag
// run, the only one the replay fixture can write. An interview run reaches the same question, since
// it sits after the one branch that builds the roll's arguments in Program.cs.
[Trait("Category", "Process")]
public class KeepQuestionTests : IDisposable
{
    private readonly string _saveFile = Path.Combine(Path.GetTempPath(), $"npcforge-keep-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_saveFile);

    private async Task<ConsoleProcess.Result> RunAsync(string input)
    {
        var flags = JsonSerializer.Deserialize<string[]>(File.ReadLines(RecordedRun.Fixture).First())!;
        return await ConsoleProcess.RunAsync(
            [.. flags, "--provider", "replay", "--recording", RecordedRun.Fixture],
            input: input, environment: new() { ["NPCFORGE_CHARACTERS"] = _saveFile });
    }

    private JsonObject Saves() => JsonNode.Parse(File.ReadAllText(_saveFile))!.AsObject();

    [Fact]
    public async Task Save_saves_the_character_prints_its_number_and_exits_0()
    {
        var run = await RunAsync("save\n");

        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Contains(KeepQuestion.Prompt, run.Stderr);
        Assert.Contains($"[app] saved as 1 in {_saveFile}", run.Stderr);
        Assert.Equal(["1"], Saves().Select(save => save.Key));
        Assert.Equal(Saves()["1"]!["text"]!.GetValue<string>().Trim(), run.Stdout.Trim());
    }

    [Fact]
    public async Task Leave_saves_nothing_says_so_and_exits_0()
    {
        var run = await RunAsync("leave\n");

        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Contains("[app] not saved", run.Stderr);
        Assert.DoesNotContain("saved as", run.Stderr);
        Assert.False(File.Exists(_saveFile));
        // The character was shown before the question, so a leave is a choice, not a failed run.
        Assert.NotEqual("", run.Stdout.Trim());
    }

    [Fact]
    public async Task End_of_input_right_after_the_character_saves_nothing_and_says_so()
    {
        var run = await RunAsync("");

        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Contains("[app] not saved: end of input", run.Stderr);
        Assert.DoesNotContain("saved as", run.Stderr);
        Assert.False(File.Exists(_saveFile));
    }

    [Fact]
    public async Task An_unrecognised_answer_asks_again_and_a_later_save_saves_exactly_once()
    {
        var run = await RunAsync("sve\nsave\n");

        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Contains("[app] \"sve\" is not save or leave.", run.Stderr);
        Assert.Equal(2, run.Stderr.Split(KeepQuestion.Prompt).Length - 1);
        Assert.Single(Saves());
        Assert.Contains("[app] saved as 1", run.Stderr);
    }

    // A typo must never throw away a paid character: none of these is an answer, so the run goes on
    // to the leave at the end and each is named once.
    [Theory]
    [InlineData("")]
    [InlineData("s")]
    [InlineData("l")]
    [InlineData("again")]
    [InlineData("yes")]
    public async Task Only_save_and_leave_are_answers_a_blank_line_or_a_first_letter_is_not(string answer)
    {
        var run = await RunAsync($"{answer}\nleave\n");

        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Contains($"[app] \"{answer}\" is not save or leave.", run.Stderr);
        Assert.Contains("[app] not saved", run.Stderr);
        Assert.False(File.Exists(_saveFile));
    }

    [Theory]
    [InlineData("  SAVE  ")]
    [InlineData("Save")]
    public async Task Save_is_accepted_trimmed_and_in_any_case(string answer)
    {
        var run = await RunAsync($"{answer}\n");

        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Contains("[app] saved as 1", run.Stderr);
        Assert.Single(Saves());
    }

    [Fact]
    public async Task Leave_is_accepted_trimmed_and_in_any_case()
    {
        var run = await RunAsync("  LEAVE \n");

        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Contains("[app] not saved", run.Stderr);
        Assert.False(File.Exists(_saveFile));
    }
}
