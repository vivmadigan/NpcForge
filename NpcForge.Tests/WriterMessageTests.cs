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

    [Fact]
    public async Task The_writer_is_sent_the_scene_word_for_word_with_the_brief()
    {
        var model = new KeepsWhatItIsSent();

        await ChatAgent.WriteAsync(model, new ChatOptions(), new FakeToolSource(),
            new WriterInput(Brief, ControlScene, WriterInput.DefaultSkillDir), CancellationToken.None);

        // One message of facts after the skill, as before: the scene joins it, not a message of its own.
        var sent = model.Messages;
        Assert.Equal([ChatRole.System, ChatRole.User], sent.Select(m => m.Role));
        var facts = sent[1].Text;
        Assert.Contains($"<scene>\n{ControlScene}\n</scene>", facts);
        Assert.Contains("the brief wins", facts);
        // The brief stays last, where ReplayTests looks for it.
        Assert.EndsWith(Brief, facts);
    }

    // A flag run has no scene. Its message is the one the replay fixture recorded, to the byte.
    [Fact]
    public async Task A_run_with_no_scene_sends_the_writer_todays_message()
    {
        var model = new KeepsWhatItIsSent();

        await ChatAgent.WriteAsync(model, new ChatOptions(), new FakeToolSource(),
            new WriterInput(Brief, Scene: null, WriterInput.DefaultSkillDir), CancellationToken.None);

        Assert.Equal($"Write this character. The brief is settled; do not change it.\n\n{Brief}", model.Messages[1].Text);
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
                new WriterInput(Brief, Scene: null, dir), CancellationToken.None);

            Assert.Equal(edited.TrimEnd() + "\n\nCOPIED COMIC TONE\n", model.Messages[0].Text);
        }
        finally { Directory.Delete(dir, recursive: true); }
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
            new WriterInput(brief, Scene: null, WriterInput.DefaultSkillDir), CancellationToken.None);

        var system = model.Messages[0].Text;
        var skill = File.ReadAllText(Path.Combine(WriterInput.DefaultSkillDir, "SKILL.md"));
        var toneFile = File.ReadAllText(ToneFiles.PathOf(WriterInput.DefaultSkillDir, tone));
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
        var sent = File.ReadAllText(path).TrimEnd() + "\n\n" + File.ReadAllText(Path.Combine(dir, "tones", "Comic.md"));
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
