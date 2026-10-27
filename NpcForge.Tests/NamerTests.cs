using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// The namer (ADR-009 decision 4) with a scripted stand-in model: what it is told, what give_name
// answers, which name wins, and when it gives up. Free and in process.
public class NamerTests
{
    private const string Brief =
        """{"setting":"a mill","occupation":"miller","tone":"Comic","race":"dwarf","nameLetter":"K","nameSyllables":"2","familyLetter":"V"}""";

    private static WriterInput Input(string? scene = null) => new(Brief, scene, WriterInput.DefaultSkillDir);

    private static Task<NamedCharacter> Name(NamerStandIn model, string? scene = null) =>
        Namer.RunAsync(model, new ChatOptions(), Input(scene), CancellationToken.None);

    // What the model was sent back for each of its give_name calls, in order.
    private static List<string> Results(NamerStandIn model) =>
        [.. model.NamerCalls[^1].Messages
            .SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.Result?.ToString() ?? "")];

    [Fact]
    public async Task Tobin_for_K_is_refused_once_and_then_Kessa_is_accepted()
    {
        var model = new NamerStandIn(
            new(([ "To", "bin" ], "Vale")),
            new(([ "Kes", "sa" ], "Vale")));

        var name = await Name(model);

        Assert.Equal("Kessa Vale", name.Full);
        Assert.Equal(["Kes", "sa"], name.Syllables);
        // Two give_name turns and the closing turn with no tool call, which ends the loop.
        Assert.Equal(3, model.NamerCalls.Count);
        var results = Results(model);
        Assert.Equal(2, results.Count);
        Assert.Contains("must start with K, and \"Tobin\" starts with T", results[0]);
        Assert.Equal("Accepted: Kessa Vale.", results[1]);
    }

    [Fact]
    public async Task A_family_name_with_the_wrong_letter_or_none_is_refused()
    {
        var model = new NamerStandIn(
            new(([ "Kes", "sa" ], "Mott")),
            new(([ "Kes", "sa" ], "")),
            new(([ "Kes", "sa" ], "Vale")));

        var name = await Name(model);

        Assert.Equal("Kessa Vale", name.Full);
        var results = Results(model);
        Assert.Contains("family name must start with V, and \"Mott\" starts with M", results[0]);
        Assert.Contains("no family name was given", results[1]);
        Assert.Equal("Accepted: Kessa Vale.", results[2]);
    }

    [Fact]
    public async Task A_second_accepted_name_is_refused_and_the_first_stands()
    {
        var model = new NamerStandIn(
            new(([ "Kes", "sa" ], "Vale")),
            new(([ "Ka", "ra" ], "Voss")));

        var name = await Name(model);

        Assert.Equal("Kessa Vale", name.Full);
        var results = Results(model);
        Assert.Equal("Accepted: Kessa Vale.", results[0]);
        Assert.Contains("already named Kessa Vale", results[1]);
    }

    // Two names in one turn: the first accepted name still wins.
    [Fact]
    public async Task Two_good_names_in_one_turn_keep_the_first()
    {
        var model = new NamerStandIn(new NamerStandIn.Turn(([ "Kes", "sa" ], "Vale"), ([ "Ka", "ra" ], "Voss")));

        var name = await Name(model);

        Assert.Equal("Kessa Vale", name.Full);
        Assert.Contains("already named Kessa Vale", Results(model)[1]);
    }

    [Fact]
    public async Task A_namer_that_never_follows_the_dice_stops_at_four_turns_with_the_dice_in_the_reason()
    {
        var model = NamerStandIn.Always(new(([ "To", "bin" ], "Vale")));

        var error = await Assert.ThrowsAsync<NoNameException>(() => Name(model));

        Assert.Equal("no name followed the dice (K, 2, V)", error.Message);
        Assert.Equal(4, model.NamerCalls.Count);
    }

    // A name accepted on the last turn still stands when the namer then runs out of turns.
    [Fact]
    public async Task A_name_accepted_before_the_cap_stands()
    {
        var model = NamerStandIn.Always(new(([ "Kes", "sa" ], "Vale")));

        var name = await Name(model);

        Assert.Equal("Kessa Vale", name.Full);
        Assert.Equal(4, model.NamerCalls.Count);
    }

    // A namer that answers without calling give_name has given no name.
    [Fact]
    public async Task A_namer_that_only_talks_gives_no_name()
    {
        var model = new NamerStandIn();

        await Assert.ThrowsAsync<NoNameException>(() => Name(model));
        Assert.Single(model.NamerCalls);
    }

    [Fact]
    public async Task The_namer_is_told_its_skill_the_race_the_dice_and_the_scene_and_offered_only_give_name()
    {
        var model = new NamerStandIn(new NamerStandIn.Turn(([ "Kes", "sa" ], "Vale")));

        await Name(model, scene: "A dusty mill by a river.");

        var first = model.NamerCalls[0];
        Assert.Equal(File.ReadAllText(Namer.SkillPath).ReplaceLineEndings("\n"), first.System);
        Assert.Equal(["give_name"], first.Tools);
        Assert.Equal(
            "Name this character.\n\nRace: dwarf\nFirst name: starts with K, 2 syllables\nFamily name: starts with V\n\n" +
            "This is the scene the GM described, in their own words.\n\n<scene>\nA dusty mill by a river.\n</scene>",
            first.FirstUser);
        // Told the race, the dice and the scene, and nothing else of the brief.
        Assert.DoesNotContain("miller", first.FirstUser);
        Assert.DoesNotContain("Comic", first.FirstUser);
    }

    [Fact]
    public async Task With_no_scene_the_namer_is_told_the_race_and_the_dice_only()
    {
        var model = new NamerStandIn(new NamerStandIn.Turn(([ "Kes", "sa" ], "Vale")));

        await Name(model);

        Assert.Equal(
            "Name this character.\n\nRace: dwarf\nFirst name: starts with K, 2 syllables\nFamily name: starts with V",
            model.NamerCalls[0].FirstUser);
    }

    // A Windows checkout has CRLF files and a Linux one LF. The namer is sent "\n" either way, so a
    // recording made on one machine has the namer's fingerprint of the other.
    [Fact]
    public void The_namer_is_sent_the_same_text_whatever_line_endings_the_file_has()
    {
        var dir = Directory.CreateTempSubdirectory("npcforge-namer-").FullName;
        try
        {
            var crlf = Path.Combine(dir, "crlf.md");
            var lf = Path.Combine(dir, "lf.md");
            File.WriteAllText(crlf, "Name one.\r\nTwo lines.\r\n");
            File.WriteAllText(lf, "Name one.\nTwo lines.\n");

            var fromCrlf = Namer.SystemMessage(crlf);
            var fromLf = Namer.SystemMessage(lf);

            Assert.DoesNotContain('\r', fromCrlf.Text);
            Assert.Equal(fromLf.Text, fromCrlf.Text);
            // The "(N chars, hash)" end of the [skill] line; the path before it differs by design.
            Assert.Equal(fromLf.Trace[fromLf.Trace.LastIndexOf('(')..], fromCrlf.Trace[fromCrlf.Trace.LastIndexOf('(')..]);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // The writer's options are not changed by the namer's tool list.
    [Fact]
    public async Task The_callers_options_keep_their_tools()
    {
        var options = new ChatOptions();

        await Namer.RunAsync(new NamerStandIn(new NamerStandIn.Turn(([ "Kes", "sa" ], "Vale"))), options, Input(), CancellationToken.None);

        Assert.Null(options.Tools);
    }
}
