using Microsoft.Extensions.AI;
using System.Security.Cryptography;     // SHA256
using System.Text;                      // Encoding
using System.Text.Json;                 // JsonSerializer, JsonElement

namespace NpcForge;

// One tool the console answers itself, offered to a model on its own: record_answer for the scene
// interview (ADR-007) and give_name for the namer (ADR-009). The model is offered this and nothing
// else, so it cannot reach roll_character, save_character or load_character (AGENTS.md,
// McpToolSource.AppOnly).
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

            // Only the name it was offered. roll_character and the rest fail here, by name.
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

public static class Fingerprint
{
    // The short hash in every [skill] line. The count beside it is the readable half: 2097 to 2239
    // says it grew by 142. The hash is the reliable half, because two same-length edits have the
    // same count and different hashes, and telling skill versions apart is the whole premise of
    // docs/runs. Hash what the model is sent, not the files on disk, so a BOM or line-ending change
    // on its own does not read as a different skill.
    public static string Of(string sent) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sent)))[..8].ToLowerInvariant();
}
