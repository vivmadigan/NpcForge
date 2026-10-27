using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// What the writer is sent (ADR-008), through the real ChatAgent and its real skill, with a stand-in
// model that keeps the messages it was given. Epic 7's Done when: the writer's message holds the
// scene, and a run with no scene sends exactly what it sent before.
public class WriterMessageTests
{
    // The control scene from ROADMAP.md epic 7.
    private const string ControlScene =
        "The players are in a small village. They have noticed something is off about the town and so they have gone into the town's only inn, which is empty apart from the innkeeper. They want to get information about the town from the innkeeper, but he refuses to give them anything, unless threatened with death.";

    private const string Brief = """{"setting":"town's only inn","playersWant":"information about the town","difficulty":"Wall","occupation":"innkeeper","tone":"Comic"}""";

    // A name already given, so these tests reach the writer without a namer.
    private static readonly NamedCharacter Kessa = new(["Kes", "sa"], "Vale");

    [Fact]
    public async Task The_writer_is_sent_the_scene_word_for_word_with_the_brief()
    {
        var model = new KeepsWhatItIsSent();

        await ChatAgent.WriteAsync(model, new ChatOptions(), new FakeToolSource(),
            new WriterInput(Brief, ControlScene, WriterInput.DefaultSkillDir, Kessa), CancellationToken.None);

        // One message of facts after the skill, as before: the scene joins it, not a message of its own.
        var sent = model.Messages;
        Assert.Equal([ChatRole.System, ChatRole.User], sent.Select(m => m.Role));
        var facts = sent[1].Text;
        Assert.Contains($"<scene>\n{ControlScene}\n</scene>", facts);
        Assert.Contains("the brief wins", facts);
        // The name comes after the scene and before the brief, and the brief stays last, where
        // ReplayTests looks for it (ADR-008, ADR-009).
        Assert.EndsWith($"</scene>\n\nTheir name is Kessa Vale.\n\n{Brief}", facts);
    }

    // A flag run has no scene: the name line, then the brief.
    [Fact]
    public async Task A_run_with_no_scene_tells_the_writer_the_name_before_the_brief()
    {
        var model = new KeepsWhatItIsSent();

        await ChatAgent.WriteAsync(model, new ChatOptions(), new FakeToolSource(),
            new WriterInput(Brief, Scene: null, WriterInput.DefaultSkillDir, Kessa), CancellationToken.None);

        Assert.Equal(
            $"Write this character. The brief is settled; do not change it.\n\nTheir name is Kessa Vale.\n\n{Brief}",
            model.Messages[1].Text);
    }

    // With no scene and no name, the message from before the namer, to the byte: the lab's estimate
    // still measures it.
    [Fact]
    public void With_no_scene_and_no_name_the_first_message_is_the_one_from_before_the_namer()
    {
        Assert.Equal(
            $"Write this character. The brief is settled; do not change it.\n\n{Brief}",
            ChatAgent.FirstMessage(Brief, scene: null, name: null));
    }

    private const string BriefWithDice =
        """{"setting":"a mill","occupation":"miller","tone":"Comic","race":"dwarf","nameLetter":"K","nameSyllables":"2","familyLetter":"V"}""";

    // With no name on the input, WriteAsync runs the namer first and tells the writer the name it
    // accepted, before the brief.
    [Fact]
    public async Task With_no_name_the_namer_runs_first_and_its_name_reaches_the_writer_before_the_brief()
    {
        var model = new NamerStandIn(new NamerStandIn.Turn((["To", "bin"], "Vale")), new NamerStandIn.Turn((["Kes", "sa"], "Vale")));

        var written = await ChatAgent.WriteAsync(model, new ChatOptions(), new FakeToolSource(),
            new WriterInput(BriefWithDice, Scene: null, WriterInput.DefaultSkillDir), CancellationToken.None);

        Assert.Equal("Kessa Vale", written.Name.Full);
        Assert.Equal("A character.", written.Text);
        Assert.Equal(3, model.NamerCalls.Count);
        var writer = Assert.Single(model.WriterCalls);
        Assert.Equal(
            $"Write this character. The brief is settled; do not change it.\n\nTheir name is Kessa Vale.\n\n{BriefWithDice}",
            writer.FirstUser);
        // The writer keeps its own tools: give_name never reaches it.
        Assert.Equal(["LookupArchetype"], writer.Tools);
    }

    [Fact]
    public async Task A_name_on_the_input_skips_the_namer()
    {
        var model = new NamerStandIn();

        var written = await ChatAgent.WriteAsync(model, new ChatOptions(), new FakeToolSource(),
            new WriterInput(BriefWithDice, Scene: null, WriterInput.DefaultSkillDir, Kessa), CancellationToken.None);

        Assert.Empty(model.NamerCalls);
        Assert.Same(Kessa, written.Name);
    }

    // No name, no writer: a namer that never follows the dice stops the run at four turns, and the
    // writer is never called (ADR-009).
    [Fact]
    public async Task A_namer_that_never_follows_the_dice_stops_the_run_and_the_writer_is_never_called()
    {
        var model = NamerStandIn.Always(new NamerStandIn.Turn((["To", "bin"], "Vale")));

        var error = await Assert.ThrowsAsync<NoNameException>(() => ChatAgent.WriteAsync(model, new ChatOptions(), new FakeToolSource(),
            new WriterInput(BriefWithDice, Scene: null, WriterInput.DefaultSkillDir), CancellationToken.None));

        Assert.Equal("no name followed the dice (K, 2, V)", error.Message);
        Assert.Equal(4, model.NamerCalls.Count);
        Assert.Empty(model.WriterCalls);
    }

    // The skill folder is a choice of the input, not a constant: a copy with an edit is what the
    // writer is told (ADR-009, one way to write). Its tones folder travels with it.
    [Fact]
    public async Task A_skill_folder_in_the_input_is_the_system_message()
    {
        var dir = Directory.CreateTempSubdirectory("npcforge-skill-").FullName;
        try
        {
            var edited = File.ReadAllText(Path.Combine(WriterInput.DefaultSkillDir, "SKILL.md")) + "\nEDITED COPY MARKER\n";
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), edited);
            Directory.CreateDirectory(Path.Combine(dir, "tones"));
            File.WriteAllText(ToneFiles.PathOf(dir, "Comic"), "COPIED COMIC TONE\n");
            var model = new KeepsWhatItIsSent();

            await ChatAgent.WriteAsync(model, new ChatOptions(), new FakeToolSource(),
                new WriterInput(Brief, Scene: null, dir, Kessa), CancellationToken.None);

            Assert.Equal((edited.TrimEnd() + "\n\nCOPIED COMIC TONE\n").ReplaceLineEndings("\n"), model.Messages[0].Text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // A Windows checkout has CRLF files and a Linux one LF. The writer is sent "\n" either way, so
    // one commit sends the same text, and prints the same [skill] fingerprint, on both machines.
    [Fact]
    public async Task The_writer_is_sent_the_same_text_whatever_line_endings_the_files_have()
    {
        var crlf = Directory.CreateTempSubdirectory("npcforge-skill-").FullName;
        var lf = Directory.CreateTempSubdirectory("npcforge-skill-").FullName;
        try
        {
            foreach (var (dir, newline) in new[] { (crlf, "\r\n"), (lf, "\n") })
            {
                File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"Skill line one.{newline}Skill line two.{newline}");
                Directory.CreateDirectory(Path.Combine(dir, "tones"));
                File.WriteAllText(ToneFiles.PathOf(dir, "Comic"), $"## Tone: Comic{newline}{newline}Be funny.{newline}");
            }

            var fromCrlf = ChatAgent.SystemMessage(crlf, Brief);
            var fromLf = ChatAgent.SystemMessage(lf, Brief);

            Assert.DoesNotContain('\r', fromCrlf.Text);
            Assert.Equal(fromLf.Text, fromCrlf.Text);
            Assert.Equal(Fingerprint(fromLf.Trace), Fingerprint(fromCrlf.Trace));
        }
        finally
        {
            Directory.Delete(crlf, recursive: true);
            Directory.Delete(lf, recursive: true);
        }

        // The "(N chars, hash)" end of the [skill] line; the folder path before it differs by design.
        static string Fingerprint(string trace) => trace[trace.LastIndexOf('(')..];
    }

    // Code reads the tone from the brief and sends that tone's file and no other (ADR-009): the
    // model cannot blend tones it was never shown. Each file is told apart by its heading.
    [Theory]
    [InlineData("Serious")]
    [InlineData("Comic")]
    [InlineData("Heartfelt")]
    public async Task The_writer_is_sent_the_skill_and_only_the_tone_file_the_brief_names(string tone)
    {
        var model = new KeepsWhatItIsSent();
        var brief = Brief.Replace("\"tone\":\"Comic\"", $"\"tone\":\"{tone}\"");

        await ChatAgent.WriteAsync(model, new ChatOptions(), new FakeToolSource(),
            new WriterInput(brief, Scene: null, WriterInput.DefaultSkillDir, Kessa), CancellationToken.None);

        var system = model.Messages[0].Text;
        var skill = File.ReadAllText(Path.Combine(WriterInput.DefaultSkillDir, "SKILL.md")).ReplaceLineEndings("\n");
        var toneFile = File.ReadAllText(ToneFiles.PathOf(WriterInput.DefaultSkillDir, tone)).ReplaceLineEndings("\n");
        Assert.StartsWith(skill.TrimEnd() + "\n\n", system);
        Assert.EndsWith(toneFile, system);
        foreach (var other in new[] { "Serious", "Comic", "Heartfelt" }.Where(name => name != tone))
            Assert.DoesNotContain($"## Tone: {other}", system);
    }

    // The line docs/runs records are compared by. Built here by hand, so a change to the format
    // fails this test instead of silently re-fingerprinting every record.
    [Fact]
    public void The_skill_trace_for_the_default_folder_keeps_its_format()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "skills", "npc-writer");
        var path = Path.Combine(dir, "SKILL.md");
        var sent = (File.ReadAllText(path).TrimEnd() + "\n\n" + File.ReadAllText(Path.Combine(dir, "tones", "Comic.md")))
            .ReplaceLineEndings("\n");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(sent)))[..8].ToLowerInvariant();

        var system = ChatAgent.SystemMessage(WriterInput.DefaultSkillDir, Brief);

        Assert.Equal($"[skill] {path} + tones/Comic.md ({sent.Length} chars, {hash})", system.Trace);
        Assert.Equal(sent, system.Text);
    }

    // A stand-in writer: answers at once, and keeps the messages of its one call.
    private sealed class KeepsWhatItIsSent : IChatClient
    {
        public List<ChatMessage> Messages { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Messages.AddRange(messages);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "A character.")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
