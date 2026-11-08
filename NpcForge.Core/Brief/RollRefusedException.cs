namespace NpcForge;

// A roll the dice refuse before any die is cast: a blank field, or an enum number that is not one of its
// values. The core cannot throw McpException, so each front end turns this into its own refusal. The
// reason is the text the GM or the app is shown.
public sealed class RollRefusedException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}
