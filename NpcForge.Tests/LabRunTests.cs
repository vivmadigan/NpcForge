using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NpcForge.Tests;

// Console.Error is a process-wide setting and the blind lab swaps it, so these tests must not run
// beside any other test that writes a trace.
[CollectionDefinition("Console.Error", DisableParallelization = true)]
public class ConsoleErrorCollection;

// Lab.RunAsync against the real server and a stand-in model (ADR-009 build order 4). No paid call
// is possible: the stand-in is the only IChatClient, and NPCFORGE_OFFLINE=1 is set for the run.
// Each test names its own temp save file and lab folder, never the owner's characters.json.
[Collection("Console.Error")]
public class LabRunTests : IDisposable
{
    private const int Seed = 5;
    private readonly List<string> _cleanup = [];

    public void Dispose()
    {
        foreach (var path in _cleanup)
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else File.Delete(path);
        }
    }

    // A skill folder whose first line names it, so the stand-in can say which one it was sent. The
    // folder is called exactly `name`, because the folder's name is its label on the sheet.
    private string Skill(string name)
    {
        var parent = Directory.CreateTempSubdirectory("npcforge-labskill-").FullName;
        _cleanup.Add(parent);
        var dir = Directory.CreateDirectory(Path.Combine(parent, name)).FullName;
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"SKILL-{name}\nWrite a character.\n");
        return dir;
    }

    private string TempFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"npcforge-labout-{Guid.NewGuid():N}");
        _cleanup.Add(dir);
        return dir;
    }

    private const string RestoredMarker = "<console-error-restored>";

    private sealed record Result(string? Folder, string Stderr, int Calls);

    // Runs the lab the way Program.cs does, with the stand-in as the whole model. Stderr is captured
    // from after the server is up, so only the lab's own lines and the variants' traces are in it.
    private async Task<Result> RunLab(
        LabStandIn model, string[] labFlags, string input, string? out_ = null, IChatClient? client = null)
    {
        var save = Path.Combine(Path.GetTempPath(), $"npcforge-labsave-{Guid.NewGuid():N}.json");
        _cleanup.Add(save);
        var run = RunOptions.Parse(
            [.. labFlags, "--seed", Seed.ToString(), "--setting", "a farm", "--out", out_ ?? TempFolder()]);

        await using var tools = new McpToolSource(save, run.Seed);
        await tools.ConnectAsync(CancellationToken.None);

        var options = new ChatOptions { ModelId = "stand-in", MaxOutputTokens = 5000 };
        var capture = new StringWriter();
        var before = Console.Error;
        Console.SetError(capture);
        string? folder;
        try
        {
            folder = await Lab.RunAsync(client ?? model, options, tools, run, new StringReader(input), CancellationToken.None);
        }
        finally
        {
            // Straight after RunAsync, before this helper puts anything back: the lab's own finally
            // must have restored Console.Error, or this test would hide a lab that left it silent.
            // Written through Console.Error and read from the capture, because SetError wraps the
            // writer and the two are never the same object.
            Console.Error.Write(RestoredMarker);
            var restored = capture.ToString().Contains(RestoredMarker);
            Console.SetError(before);
            Assert.True(restored, "Lab.RunAsync left Console.Error pointing somewhere else");
        }

        // A lab saves nothing: the temp save file was never created.
        Assert.False(File.Exists(save), "a lab run must not save a character");
        Assert.Null(options.Temperature);
        return new Result(folder, capture.ToString(), model.Calls.Count);
    }

    [Fact]
    public async Task A_skill_lab_writes_every_variant_and_take_and_each_skill_reaches_the_writer()
    {
        var model = new LabStandIn();
        var alpha = Skill("alpha");
        var beta = Skill("beta");

        var result = await RunLab(model, ["--lab", $"skill={alpha},{beta}", "--repeat", "2"], "yes\n");

        var sheet = File.ReadAllText(Path.Combine(result.Folder!, "sheet.md"));
        Assert.Equal(4, model.Calls.Count);
        // The stand-in answers with the first line of the system message it was sent.
        Assert.Equal(["SKILL-alpha", "SKILL-alpha", "SKILL-beta", "SKILL-beta"], model.Calls.Select(c => c.SystemFirstLine));
        foreach (var (label, take) in new[] { ("alpha", 1), ("alpha", 2), ("beta", 1), ("beta", 2) })
        {
            var section = sheet[sheet.IndexOf($"## {label}, take {take}", StringComparison.Ordinal)..];
            var body = section.Split("\n## ")[0];
            Assert.Contains($"TEXT(SKILL-{label}", body);
            // The sheet shows the skill line the writer was given.
            Assert.Contains("[skill] ", body);
        }
        Assert.Single(Regex.Matches(sheet, @"""setting"":"));
        Assert.False(File.Exists(Path.Combine(result.Folder!, "blind.md")));
        Assert.Contains("[lab] 4 characters (2 variants x 2 takes), written by gpt-6-luna.", result.Stderr);
        Assert.Contains("[lab] written to ", result.Stderr);
    }

    [Fact]
    public async Task A_temperature_lab_sets_the_temperature_on_a_copy_of_the_options()
    {
        var model = new LabStandIn();

        var result = await RunLab(model, ["--lab", "temperature=0.2,0.9"], "yes\n");

        // RunLab asserts the caller's own options were left alone.
        Assert.Equal([0.2f, 0.9f], model.Calls.Select(c => c.Temperature));
        var sheet = File.ReadAllText(Path.Combine(result.Folder!, "sheet.md"));
        Assert.Contains("## 0.2, take 1", sheet);
        Assert.Contains("## 0.9, take 1", sheet);
    }

    private static readonly string[] SixNames = ["a", "b", "c", "d", "e", "f"];

    private string[] SixSkills() => [.. SixNames.Select(Skill)];

    [Fact]
    public async Task Blind_writes_a_sheet_and_a_key_that_agree_and_no_side_sheet()
    {
        var model = new LabStandIn();
        var skills = SixSkills();

        var result = await RunLab(model, ["--lab", $"skill={string.Join(',', skills)}", "--mode", "blind"], "yes\n");

        Assert.False(File.Exists(Path.Combine(result.Folder!, "sheet.md")));
        var blind = File.ReadAllText(Path.Combine(result.Folder!, "blind.md"));
        var key = LabSheet.ReadKey(File.ReadAllText(Path.Combine(result.Folder!, "key.json")));
        Assert.Equal(["A", "B", "C", "D", "E", "F"], key.Keys.Order());
        foreach (var (letter, label) in key)
        {
            var text = Regex.Match(blind, $@"## Character {letter}\r?\n\r?\nTEXT\(SKILL-(\w)").Groups[1].Value;
            Assert.Equal(label, text);
        }
        Assert.DoesNotContain("[skill]", blind);
        Assert.DoesNotContain("npcforge-labskill", blind);

        // The run order is Shuffle(variants) from new Random(seed), and the letters come next from the
        // same Random, so a test can predict both without running the lab.
        var random = new Random(Seed);
        var runOrder = LabSheet.Shuffle(SixNames, random);
        Assert.Equal(runOrder.Select(name => $"SKILL-{name}"), model.Calls.Select(c => c.SystemFirstLine));
        var lettered = LabSheet.Shuffle(runOrder, random);
        Assert.Equal(lettered, key.OrderBy(pair => pair.Key).Select(pair => pair.Value));
    }

    [Fact]
    public async Task Blind_prints_no_trace_while_it_runs_and_the_same_seed_gives_the_same_key()
    {
        var skills = $"skill={string.Join(',', SixSkills())}";
        var first = await RunLab(new LabStandIn { CallsATool = true }, ["--lab", skills, "--mode", "blind"], "yes\n");
        var second = await RunLab(new LabStandIn { CallsATool = true }, ["--lab", skills, "--mode", "blind"], "yes\n");

        // Nothing a variant prints can give the key away: no label, no skill path, no token count.
        foreach (var stderr in new[] { first.Stderr, second.Stderr })
        {
            Assert.DoesNotContain("[loop]", stderr);
            Assert.DoesNotContain("[skill]", stderr);
            Assert.DoesNotContain("[tool]", stderr);
            // Not [server]: the server's startup and roll lines arrive on their own thread before
            // the window opens, and are not the variants'. The side control below does see them.
            Assert.DoesNotContain("npcforge-labskill", stderr);
            // The lab's own lines are outside the window: the confirmation and the folder.
            Assert.Contains("Run this lab? Type yes to run it.", stderr);
            Assert.Contains("[lab] written to ", stderr);
        }
        Assert.Equal(
            File.ReadAllText(Path.Combine(first.Folder!, "key.json")),
            File.ReadAllText(Path.Combine(second.Folder!, "key.json")));
        Assert.Equal(
            File.ReadAllText(Path.Combine(first.Folder!, "blind.md")),
            File.ReadAllText(Path.Combine(second.Folder!, "blind.md")));
    }

    // A failed variant is the one thing the lab reports after the silent window. Its label would be
    // the key, so stderr gets a count only, and the label, take and reason go to key.json, which the
    // GM reads after guessing.
    [Fact]
    public async Task Blind_with_a_failing_variant_prints_no_label_and_keeps_the_failure_in_key_json()
    {
        var names = new[] { "gentle", "terse", "wry" };
        var model = new LabStandIn
        {
            Behave = call => call.SystemFirstLine == "SKILL-terse"
                ? throw new InvalidOperationException("HTTP 400 on terse")
                : null,
        };

        var result = await RunLab(model, ["--lab", $"skill={string.Join(',', names.Select(Skill))}", "--mode", "blind"], "yes\n");

        foreach (var secret in new[] { "gentle", "terse", "wry", "HTTP 400", "npcforge-labskill" })
            Assert.DoesNotContain(secret, result.Stderr);
        Assert.Contains("[lab] 1 character failed to write; the reasons are in key.json", result.Stderr);

        var key = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.Folder!, "key.json"))).RootElement;
        var failure = Assert.Single(key.GetProperty("failures").EnumerateArray());
        Assert.Equal("terse", failure.GetProperty("label").GetString());
        Assert.Equal(1, failure.GetProperty("take").GetInt32());
        Assert.Contains("HTTP 400 on terse", failure.GetProperty("reason").GetString());
        Assert.Equal(2, LabSheet.ReadKey(File.ReadAllText(Path.Combine(result.Folder!, "key.json"))).Count);
        // blind.md says the same thing as stderr, and nothing more.
        var blind = File.ReadAllText(Path.Combine(result.Folder!, "blind.md"));
        Assert.Contains("1 character failed to write; the reasons are in key.json", blind);
        Assert.DoesNotContain("terse", blind);
    }

    // The positive control for the test above: the same lab in side mode does print those lines into
    // this capture, so their absence in blind mode is the window's doing and not the capture's.
    [Fact]
    public async Task A_side_lab_prints_its_trace_into_the_same_capture()
    {
        var result = await RunLab(
            new LabStandIn { CallsATool = true }, ["--lab", $"skill={string.Join(',', SixSkills())}"], "yes\n");

        Assert.Contains("[loop]", result.Stderr);
        Assert.Contains("[skill]", result.Stderr);
        Assert.Contains("[tool] lookup_archetype", result.Stderr);
        Assert.Contains("[server]", result.Stderr);
    }

    // The cap is caught inside the lab, so it never tests the finally. Cancellation is the one
    // exception that escapes the loop, so it is what shows the restore runs when the lab is cut off.
    // RunLab asserts Console.Error is the capture again as soon as RunAsync has thrown.
    [Fact]
    public async Task Console_Error_is_back_after_a_blind_lab_is_cancelled_mid_run()
    {
        var skills = SixSkills();
        var model = new LabStandIn { Behave = _ => throw new OperationCanceledException() };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RunLab(model, ["--lab", $"skill={skills[0]},{skills[1]}", "--mode", "blind"], "yes\n"));

        Assert.Single(model.Calls);
    }

    [Fact]
    public async Task Console_Error_is_back_after_a_blind_lab_that_hit_the_cap()
    {
        var skills = SixSkills();
        var model = new LabStandIn { Behave = _ => throw new SpendingCapReachedException(1, 1) };

        await RunLab(model, ["--lab", $"skill={skills[0]},{skills[1]}", "--mode", "blind"], "yes\n");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no\n")]
    [InlineData("y\n")]
    [InlineData("yes please\n")]
    [InlineData("\n")]
    public async Task Anything_but_yes_stops_before_any_model_call(string input)
    {
        var model = new LabStandIn();
        var folder = TempFolder();

        var result = await RunLab(model, ["--lab", $"skill={Skill("a")},{Skill("b")}"], input, folder);

        Assert.Null(result.Folder);
        Assert.Equal(0, result.Calls);
        Assert.False(Directory.Exists(folder));
        Assert.Contains("Run this lab? Type yes to run it. Anything else stops before any model call.", result.Stderr);
        Assert.Contains("[lab] not run: the GM did not say yes", result.Stderr);
        // The estimate was shown before the question, labelled "about", in tokens.
        Assert.Matches(
            @"\[lab\] About [\d,]+ tokens, against the session cap of 200,000\. The cap stops the lab if it runs over\.",
            result.Stderr);
        Assert.DoesNotContain("at most", result.Stderr);
    }

    [Fact]
    public async Task Yes_in_any_case_runs_it()
    {
        var model = new LabStandIn();

        var result = await RunLab(model, ["--lab", $"skill={Skill("a")},{Skill("b")}"], " YES \n");

        Assert.Equal(2, result.Calls);
        Assert.NotNull(result.Folder);
    }

    [Fact]
    public async Task An_estimate_over_the_cap_is_refused_without_reading_input()
    {
        var model = new LabStandIn();

        // 2 variants x 14 takes, each 2 calls of about 7,000 tokens: well over 200,000.
        var result = await RunLab(model, ["--lab", $"skill={Skill("a")},{Skill("b")}", "--repeat", "14"], "yes\n");

        Assert.Null(result.Folder);
        Assert.Equal(0, result.Calls);
        Assert.Contains("[lab] not run: that is over the session cap, so no model call is made.", result.Stderr);
        Assert.DoesNotContain("Run this lab?", result.Stderr);
    }

    [Fact]
    public async Task The_estimate_is_two_calls_of_system_first_message_and_max_output_per_character()
    {
        var model = new LabStandIn();

        var result = await RunLab(model, ["--lab", $"skill={Skill("a")},{Skill("b")}"], "yes\n");

        // The stand-in saw what each call was really sent, so the printed estimate is checked against
        // those lengths and MaxOutputTokens (5000) and not against a copy of the formula's inputs.
        var expected = model.Calls.Sum(call => 2L * (call.SystemLength + call.UserLength + 5000));
        var printed = long.Parse(Regex.Match(result.Stderr, @"About ([\d,]+) tokens").Groups[1].Value.Replace(",", ""));
        Assert.Equal(expected, printed);
    }

    [Fact]
    public async Task A_failing_variant_stays_on_the_sheet_with_its_reason_and_the_lab_goes_on()
    {
        var model = new LabStandIn
        {
            Behave = call => call.Temperature == 0.9f
                ? throw new InvalidOperationException("HTTP 400: temperature is not supported by this model")
                : null,
        };

        var result = await RunLab(model, ["--lab", "temperature=0.2,0.9,0.5"], "yes\n");

        var sheet = File.ReadAllText(Path.Combine(result.Folder!, "sheet.md"));
        Assert.Equal(3, model.Calls.Count);
        Assert.Contains("**Failed.** HTTP 400: temperature is not supported by this model", sheet);
        Assert.Contains("## 0.5, take 1", sheet);
        Assert.Contains("TEXT(", sheet[sheet.IndexOf("## 0.5, take 1", StringComparison.Ordinal)..]);
        Assert.DoesNotContain("stopped at the spending cap", sheet);
    }

    [Fact]
    public async Task The_cap_reached_mid_lab_stops_it_and_the_sheet_says_where()
    {
        var model = new LabStandIn { TokensPerCall = 100 };
        var capped = new SpendingCap(model, maxTokens: 250);

        var result = await RunLab(model, ["--lab", $"skill={string.Join(',', SixSkills())}"], "yes\n", client: capped);

        var sheet = File.ReadAllText(Path.Combine(result.Folder!, "sheet.md"));
        // 100 + 100 + 100 reaches the cap after the third call; the fourth is refused before it is sent.
        Assert.Equal(3, model.Calls.Count);
        Assert.Contains("stopped at the spending cap after 3 of 6", sheet);
        Assert.Contains("## a, take 1", sheet);
        Assert.DoesNotContain("## d, take 1", sheet);
        Assert.Contains("[lab] stopped at the spending cap after 3 of 6", result.Stderr);
    }

    [Fact]
    public async Task A_folder_that_already_holds_a_lab_is_refused_before_any_call()
    {
        var folder = TempFolder();
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "sheet.md"), "an old sheet");
        var model = new LabStandIn();

        var result = await RunLab(model, ["--lab", $"skill={Skill("a")},{Skill("b")}", "--mode", "blind"], "yes\n", folder);

        Assert.Null(result.Folder);
        Assert.Equal(0, result.Calls);
        Assert.Contains("already holds a lab", result.Stderr);
        Assert.False(File.Exists(Path.Combine(folder, "blind.md")));
    }

    // The folder is made before the question, so a --out that cannot be used fails while the lab
    // is still free and not after every paid call.
    [Fact]
    public async Task An_out_that_is_an_existing_file_stops_before_any_model_call_with_a_reason()
    {
        var model = new LabStandIn();
        var file = TempFolder();
        File.WriteAllText(file, "not a folder");

        var error = await Assert.ThrowsAnyAsync<IOException>(
            () => RunLab(model, ["--lab", $"skill={Skill("a")},{Skill("b")}"], "yes\n", file));

        Assert.Contains(file, error.Message);
        Assert.Empty(model.Calls);
    }

    [Fact]
    public async Task A_skill_folder_that_is_missing_stops_the_lab_before_any_call()
    {
        var model = new LabStandIn();
        var missing = Path.Combine(Path.GetTempPath(), $"npcforge-nosuch-{Guid.NewGuid():N}");

        var error = await Assert.ThrowsAnyAsync<IOException>(
            () => RunLab(model, ["--lab", $"skill={Skill("a")},{missing}"], "yes\n"));

        Assert.Contains(missing, error.Message);
        Assert.Empty(model.Calls);
    }

    // The stand-in model: answers at once with a text that names the system message it was sent,
    // and keeps what each call was given. `Behave` may throw, or return text to say instead.
    private sealed class LabStandIn : IChatClient
    {
        public sealed record Call(string SystemFirstLine, float? Temperature, int SystemLength, int UserLength);

        public List<Call> Calls { get; } = [];
        public Func<Call, string?>? Behave { get; init; }
        public int TokensPerCall { get; init; }

        // True: the first call of each character asks for lookup_archetype, and the call after the
        // tool's answer writes the text. That is a real [tool] line (and a [server] line) for the
        // blind window to hide.
        public bool CallsATool { get; init; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (CallsATool && messages.Last().Role != ChatRole.Tool)
            {
                var ask = new FunctionCallContent("call-1", "lookup_archetype",
                    new Dictionary<string, object?> { ["occupation"] = "innkeeper" });
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [ask])));
            }

            var system = messages.First(m => m.Role == ChatRole.System).Text;
            var user = messages.First(m => m.Role == ChatRole.User).Text;
            var call = new Call(system.Split('\n')[0], options?.Temperature, system.Length, user.Length);
            Calls.Add(call);

            var text = Behave?.Invoke(call) ?? $"TEXT({call.SystemFirstLine}{(call.Temperature is { } t ? $", temperature {t}" : "")})";
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
            if (TokensPerCall > 0)
                response.Usage = new UsageDetails { TotalTokenCount = TokensPerCall };
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
