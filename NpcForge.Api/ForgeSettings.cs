namespace NpcForge.Api;

// The settings that pick the forge's model and dice. The host reads every environment variable as a
// setting, unprefixed, so a bare name such as Model would take a variable some other tool set. These
// carry the app's prefix, as NPCFORGE_CHARACTERS and NPCFORGE_OFFLINE do.
public static class ForgeSettings
{
    // Default to the console's, ModelClients.DefaultProvider and DefaultModel (ADR-019, decision 6).
    public const string Provider = "NPCFORGE_PROVIDER";
    public const string Model = "NPCFORGE_MODEL";

    // Fixes every interview's dice, as --seed fixes the console's. Unset is random.
    public const string Seed = "NPCFORGE_SEED";
}
