using System.Text.Json;
using System.Text.Json.Nodes;           // JsonObject, JsonNode

namespace NpcForge.Tests;

// ADR-013, decisions 1 and 2: after the character is shown the app asks, and only "save" saves.
// "again" is RollAgainTests' part.
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
        // Not compared with the save: a child's stdout is not byte-exact (lesson 006).
        Assert.NotEqual("", run.Stdout.Trim());
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
        Assert.Contains("[app] \"sve\" is not save, again or leave.", run.Stderr);
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
    [InlineData("a")]
    [InlineData("yes")]
    public async Task Only_the_three_words_are_answers_a_blank_line_or_a_first_letter_is_not(string answer)
    {
        var run = await RunAsync($"{answer}\nleave\n");

        Assert.True(run.ExitCode == 0, run.Stderr);
        Assert.Contains($"[app] \"{answer}\" is not save, again or leave.", run.Stderr);
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

// The question alone, in process: what each answer is read as. No server and no app, so these are in
// the fast part; the tests above prove the app acts on the answer.
public class KeepAnswerTests
{
    private static async Task<(Keep Choice, string Dialogue)> AskAsync(string input)
    {
        var dialogue = new StringWriter();
        var choice = await KeepQuestion.AskAsync(new StringReader(input), dialogue, CancellationToken.None);
        return (choice, dialogue.ToString());
    }

    [Theory]
    [InlineData("save\n", Keep.Save)]
    [InlineData("again\n", Keep.Again)]
    [InlineData(" AGAIN \n", Keep.Again)]
    [InlineData("leave\n", Keep.Leave)]
    [InlineData("", Keep.EndOfInput)]
    // A pipe that writes a UTF-8 byte order mark puts it before the first answer, and Trim leaves it.
    [InlineData("﻿again\n", Keep.Again)]
    public async Task Each_word_is_its_own_choice_and_end_of_input_is_its_own(string input, Keep expected)
    {
        Assert.Equal(expected, (await AskAsync(input)).Choice);
    }

    [Fact]
    public async Task A_first_letter_of_again_is_not_again_and_the_question_is_asked_again()
    {
        var (choice, dialogue) = await AskAsync("a\nleave\n");

        Assert.Equal(Keep.Leave, choice);
        Assert.Contains("[app] \"a\" is not save, again or leave.", dialogue);
        Assert.Equal(2, dialogue.Split(KeepQuestion.Prompt).Length - 1);
    }
}
