using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace NpcForge
{
    // The step 2 tool, behind the interface. The real run uses this until step 5 swaps in
    // MCP; the tests keep using it after that. The loop cannot tell the difference, by design.
    public sealed class FakeToolSource : IToolSource
    {
        // The model never sees this C#. AIFunctionFactory reads the method by reflection and
        // builds the JSON it does see. The two [Description]s are the only prose in that JSON.
        [Description("Look up how a given kind of person usually behaves.")]
        static string LookupArchetype(
            [Description("An occupation, such as innkeeper or farmer")]
            string occupation)
        {
            return $"A typical {occupation} is busy, watchful, and knows everyone's business.";
        }

        // Wrapped once, here, so the name the model sees and the method that runs are the
        // same object and cannot drift apart. The wrapper is what ListAsync hands out and
        // what InvokeAsync runs.
        private readonly AIFunction _lookup = AIFunctionFactory.Create(LookupArchetype);   // moved here from ChatAgent

        // What the model is allowed to ask for: one entry, in a list, as an already-finished task.
        public Task<IReadOnlyList<AITool>> ListAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AITool>>([_lookup]);   // just the one

        // Run the call the model asked for and return the result as plain text. The call's
        // Arguments are the dictionary the model sent; the wrapper matches them to the method.
        public async Task<string> InvokeAsync(FunctionCallContent call, CancellationToken ct)
        {
            try
            {
                // A trace, on stderr so it never mixes with the answer. Step 5's server logs
                // arrive on the same channel with a [server] prefix.
                Console.Error.WriteLine($"[tool] {call.Name} {JsonSerializer.Serialize(call.Arguments)}");

                var result = await _lookup.InvokeAsync(new AIFunctionArguments(call.Arguments), ct);   // step 3's line
                return result?.ToString() ?? "";              // the JsonElement, unwrapped to text
            }
            catch (Exception ex)
            {
                return $"Tool failed: {ex.Message}";          // a result, not an exception
            }
        }
    }
}
