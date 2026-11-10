namespace NpcForge;

// A roll the dice refuse before any die is cast: a required field left out, a choice the field does not
// have, a blank field, or an enum number that is not one of its values. Each names the field. The reason
// is the text the GM is shown, and each front end shows it its own way.
public sealed class RollRefusedException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}
