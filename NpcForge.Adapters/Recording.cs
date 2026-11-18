using Microsoft.Extensions.AI;
using System.Text;                      // StringBuilder
using System.Text.Json;                 // JsonElement, JsonSerializer, JsonSerializerOptions

namespace NpcForge;

// The file --record writes and --provider replay reads. Line 1 is the run's flags. Every line
// after it is one model call: what the app sent, and what came back.
public sealed record RecordedCall(RecordedRequest Request, ChatResponse Response);

// What the app sent on one call: the messages, every ChatOptions setting, and the tools.
public sealed record RecordedRequest(List<ChatMessage> Messages, JsonElement Options, List<RecordedTool> Tools)
{
    // Copied out of the live call. The recorder and the replay both call it before the model is
    // asked, so a tool that cannot be recorded stops the run here, loud and free.
    public static RecordedRequest From(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        // Options is the library's own JSON of every ChatOptions field, so a setting nobody
        // thought to list (a Temperature, a new field in a later library) cannot change unnoticed.
        // The library leaves out Tools and RawRepresentationFactory: serialising a ChatOptions
        // that holds both writes neither. The tools are written out by hand below, because left
        // to the library a lost tool list would replay as if nothing changed.
        var snapshot = JsonSerializer.SerializeToElement(options, Recording.Json);

        // A kind this code cannot write out (a hosted web search, say) would be missing from the
        // recording and from the comparison, and the model would be shown a tool the replay never
        // checked.
        var tools = new List<RecordedTool>();
        foreach (var tool in options?.Tools ?? [])
        {
            if (tool is not AIFunctionDeclaration function)
                throw new NotSupportedException(
                    $"Cannot record tool '{tool.Name}' ({tool.GetType().Name}): a recording holds function tools only.");

            tools.Add(new RecordedTool(function.Name, function.Description, function.JsonSchema));
        }

        return new RecordedRequest([.. messages], snapshot, tools);
    }
}

// One tool as the model was shown it: everything the model knows about a tool.
public sealed record RecordedTool(string Name, string Description, JsonElement Schema);

public static class Recording
{
    // The library's own JSON settings, which know every kind of message content. Its default is
    // indented, and a JSONL file needs one call per line, so this copy writes on one line. Its
    // encoder already leaves curly quotes and dashes as written, as Storage.cs sets by hand, so
    // the fixture stays readable.
    public static readonly JsonSerializerOptions Json = new(AIJsonUtilities.DefaultOptions)
    {
        WriteIndented = false,
    };

    // The part of a request a replay compares, as text you can read in the debugger and in a
    // failed test: the options, the tools, and every message in order. The one comparison text:
    // the replay describes both sides with it, and its failure message quotes a line of it.
    public static string Describe(RecordedRequest request)
    {
        var text = new StringBuilder();
        text.AppendLine($"options: {request.Options.GetRawText()}");

        foreach (var tool in request.Tools)
            text.AppendLine($"tool: {tool.Name}, {tool.Description}, {JsonSerializer.Serialize(tool.Schema, Json)}");

        // The model's messages too. "Skip one and the next request is malformed"
        // (docs/architecture.md "The loop"), so dropping the model's turn from the history must
        // fail the replay.
        //
        // Text goes in as its own lines, so a one-word change in SKILL.md is named on its own
        // line. A tool result is its call id and text. Anything else, the model's reasoning and
        // its function calls with their arguments included, goes in as the library's JSON, type
        // marker and every property: a field added later is compared without anyone listing it.
        // Both sides of a replay come out of the recording, so the JSON of the model's own
        // content is the same on both sides unless the app changed it.
        foreach (var message in request.Messages)
        {
            text.AppendLine($"--- {message.Role}");

            foreach (var content in message.Contents)
            {
                text.AppendLine(content switch
                {
                    TextContent t => t.Text,
                    FunctionResultContent r => $"{r.CallId}: {r.Result}",
                    _ => JsonSerializer.Serialize<AIContent>(content, Json),
                });
            }
        }

        // SKILL.md is LF in git and CRLF on a Windows disk. Without this, a fresh checkout on
        // another machine would fail the replay on line endings alone.
        return text.ToString().ReplaceLineEndings("\n");
    }
}
