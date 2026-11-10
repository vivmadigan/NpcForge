using Microsoft.Extensions.AI;
using System.Text.Json;                 // JsonSerializer, JsonElement

namespace NpcForge;

// One tool the app answers itself, offered to a model on its own: record_answer for the scene
// interview (ADR-007), give_name for the namer (ADR-009) and lookup_archetype for the writer
// (CharacterRun.WriterTools). The model is offered this and nothing else. The roll, the save, the load
// and the list are plain methods, never a tool, so no model can reach them (AGENTS.md, ADR-014).
public sealed class ConsoleTool(AIFunction function) : IToolSource
{
    public Task<IReadOnlyList<AITool>> ListAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AITool>>([function]);

    // Same contract as the other sources: one [tool] trace line, and every failure comes back as
    // text the model can read.
    public async Task<string> InvokeAsync(FunctionCallContent call, CancellationToken ct)
    {
        try
        {
            Console.Error.WriteLine($"[tool] {call.Name} {JsonSerializer.Serialize(call.Arguments)}");

            // Only the name it was offered. Any other name, roll_character among them, fails here.
            if (call.Name != function.Name)
                throw new InvalidOperationException($"No tool named '{call.Name}'.");

            var result = await function.InvokeAsync(new AIFunctionArguments(call.Arguments), ct);
            var text = result is JsonElement element && element.ValueKind == JsonValueKind.String
                ? element.GetString()!
                : result?.ToString() ?? "";
            Console.Error.WriteLine($"[tool]   {text}");
            return text;
        }
        catch (Exception ex)
        {
            return $"Tool failed: {ex.Message}";          // a result, not an exception
        }
    }
}
