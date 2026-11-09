using Microsoft.Extensions.AI;
using System.ComponentModel;            // Description

namespace NpcForge;

// The writer's one tool, lookup_archetype (ADR-014, decisions 3 and 5). AIFunctionFactory reads the two
// [Description]s below into the schema the model is sent, as the MCP SDK reads them off CharacterTools. The
// server keeps a copy of both until it retires, and CoreMatchesServerTests holds the two equal, so the switch
// cannot change what the model sees.
public static class Archetypes
{
    public static AIFunction Lookup { get; } = AIFunctionFactory.Create(
        LookupArchetype, new AIFunctionFactoryOptions { Name = "lookup_archetype" });

    [Description("Look up how a given kind of person usually behaves.")]
    private static string LookupArchetype(
        [Description("An occupation, such as innkeeper or farmer")] string occupation) =>
        $"A typical {occupation} is busy, watchful, and knows everyone's business.";
}
