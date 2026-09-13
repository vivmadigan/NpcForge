using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace NpcForge
{
    // BUILD.md's loop in Microsoft.Extensions.AI types. From here on this is the whole app:
    // everything later is either a tool source it is given or text it is given. It knows
    // neither which vendor is behind the model nor where the tools come from.
    public sealed class AgentLoop(IChatClient model, IToolSource tools, ChatOptions options, int maxTurns = 8)
    {
        // history is the state. The model keeps nothing between calls, so every request resends
        // the whole list. The caller builds it and owns it; this method only ever appends.
        public async Task<string> RunAsync(List<ChatMessage> history, CancellationToken ct)
        {
            // Ask the source what exists instead of knowing. Step 5 swaps the fake for MCP here.
            options.Tools = [.. await tools.ListAsync(ct)];

            // One pass per model turn. The cap is not optional: a confused model loops until stopped.
            for (var turn = 0; turn < maxTurns; turn++)
            {
                var response = await model.GetResponseAsync(history, options, ct);
                history.AddRange(response.Messages);          // the assistant's turn goes in as-is

                // Text and tool requests arrive together. The requests are the only signal;
                // FinishReason and Text are provider-shaped and mean different things per vendor.
                var calls = response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<FunctionCallContent>()
                    .ToList();

                if (calls.Count == 0)
                    return response.Text;                     // the exit: no tool calls

                // Every call gets a result, failures included (the source returns those as text).
                // Skip one and the next request is malformed. CallId is how the model pairs
                // the answer to its request.
                foreach (var call in calls)
                {
                    var result = await tools.InvokeAsync(call, ct);
                    history.Add(new ChatMessage(ChatRole.Tool,
                        [new FunctionResultContent(call.CallId, result)]));
                }
            }

            // Out of turns and still no plain answer. Stopping here is the point of the cap.
            throw new InvalidOperationException($"No answer after {maxTurns} turns.");
        }
    }
}
