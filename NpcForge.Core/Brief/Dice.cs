namespace NpcForge;

// The dice: the whole of the variety, and the model cannot reach them.
public sealed class Dice
{
    // A die cast now, as NextDouble(), and read later against its table. A seeded Random computes
    // Next(n) as (int)(NextDouble() * n), so this picks the row Pick would have picked had the table
    // been known at the cast, and every seed keeps its brief (ADR-010). A test holds it.
    public static T ReadCast<T>(double cast, IReadOnlyList<T> table) => table[(int)(cast * table.Count)];
}
