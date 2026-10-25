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

    private const string Brief = """{"setting":"town's only inn","playersWant":"information about the town","difficulty":"Wall","occupation":"innkeeper"}""";

    [Fact]
    public async Task The_writer_is_sent_the_scene_word_for_word_with_the_brief()
    {
        var model = new KeepsWhatItIsSent();

        await ChatAgent.RunAsync(model, new ChatOptions(), new FakeToolSource(), Brief, ControlScene);

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

        await ChatAgent.RunAsync(model, new ChatOptions(), new FakeToolSource(), Brief, scene: null);

        Assert.Equal($"Write this character. The brief is settled; do not change it.\n\n{Brief}", model.Messages[1].Text);
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
