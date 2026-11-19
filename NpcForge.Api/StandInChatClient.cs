using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace NpcForge.Api;

// The model the tests and a free run of the site use, picked by the provider setting `stand-in`
// (ADR-019, decision 7). Scripted and deterministic. Which side a call is comes from what the app sends,
// the same way the tests' older stand-ins tell it: the namer by its system message, the interviewer by
// the record_answer tool it is offered, and the writer is everything else.
//
// It is free, like the `replay` provider, and only a setting picks it. The only text it reads is what
// the app wrote for a model: the scene it was sent and the namer's dice lines. It never reads a model's
// prose, because there is none (AGENTS.md, "never parse the model's prose").
public sealed class StandInChatClient : IChatClient
{
    // What every writer call returns. One text for every character, so a sheet says whose it is not.
    public const string Sheet = "## A stand-in character\n\nThis sheet came from the stand-in model, which writes the same text for every character. No model was called.";

    // Tokens each response reports, so a test can reach the spending cap. Small by default, so a whole
    // run stays far below it.
    public long TokensPerCall { get; set; } = 1_000;

    // The name that follows the dice in the namer's first message, as the namer's skill asks: the first
    // name's letter and syllable count, and the family name's letter. The one home of the rule, which
    // the tests' NamerStandIn calls too, so the two cannot disagree about what a name that follows the
    // dice is.
    public static (string[] Syllables, string Family) NameFor(string namerMessage)
    {
        var first = Regex.Match(namerMessage, @"First name: starts with (\w), (\d) syllable");
        var family = Regex.Match(namerMessage, @"Family name: starts with (\w)");
        string[] sounds = [first.Groups[1].Value + "a", "ra", "na"];
        return ([.. sounds.Take(int.Parse(first.Groups[2].Value))], family.Groups[1].Value + "ale");
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var history = messages.ToList();

        var reply = IsNamer(history) ? Namer(history)
            : options?.Tools?.Any(tool => tool.Name == "record_answer") == true ? Interviewer(history)
            : new ChatMessage(ChatRole.Assistant, Sheet);

        return Task.FromResult(new ChatResponse(reply) { Usage = new UsageDetails { TotalTokenCount = TokensPerCall } });
    }

    // Same test as the tests' NamerStandIn.IsNamer: the namer's skill is the system message.
    private static bool IsNamer(List<ChatMessage> history) =>
        history.FirstOrDefault(message => message.Role == ChatRole.System)?.Text.Contains("name: namer") == true;

    // One give_name call with the name inside the dice, then, once its result is back, a reply with no
    // call, which ends the loop.
    private static ChatMessage Namer(List<ChatMessage> history)
    {
        if (history[^1].Role == ChatRole.Tool)
            return new ChatMessage(ChatRole.Assistant, "Done.");

        var (syllables, family) = NameFor(history.First(message => message.Role == ChatRole.User).Text);
        return new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("name-1", "give_name",
                new Dictionary<string, object?> { ["syllables"] = syllables, ["family"] = family }),
        ]);
    }

    // On the turn that answers the GM's scene, one record_answer call for each choice field whose choice
    // the scene names, quoting the sentence that names it. A free-text field is never recorded, so the
    // interview asks for the ones it must. The fields come from the roll's own list, and a long choice is
    // left to the dice as the interview leaves it (SceneDraft.IsLongChoice), so no field is named here.
    private static ChatMessage Interviewer(List<ChatMessage> history)
    {
        if (history[^1].Role != ChatRole.User)
            return new ChatMessage(ChatRole.Assistant, "Done.");

        var scene = history[^1].Text;
        var calls = new List<AIContent>();
        foreach (var field in Dice.Fields.Where(f => f.Choices is not null && !SceneDraft.IsLongChoice(f)))
        {
            foreach (var choice in field.Choices!)
            {
                var named = Words.Split(choice);
                // A sentence of more than a quote's length cannot be quoted whole, so it is passed over.
                var sentence = Words.Sentences(scene).FirstOrDefault(s =>
                    s.Words.Count <= SceneDraft.MostWordsInAQuote && Words.IndexOf(s.Words, named) >= 0);
                if (sentence is null)
                    continue;

                calls.Add(new FunctionCallContent($"record-{calls.Count + 1}", "record_answer",
                    new Dictionary<string, object?> { ["field"] = field.Name, ["value"] = choice, ["quote"] = sentence.Text }));
                break;
            }
        }

        return calls.Count == 0
            ? new ChatMessage(ChatRole.Assistant, "Done.")
            : new ChatMessage(ChatRole.Assistant, calls);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The stand-in answers whole responses only; the app never streams.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
