namespace NpcForge.Api;

// The one file the API reads and writes saves in, parsed once where it enters from the outside
// (ADR-019, decision 5). Never FileCharacterStore.DefaultPath: that is three folders up from the
// running program, which is the owner's file only when the console runs. Here the AppHost, or an agent
// pointing a run at a scratch file, says which file it is.
public sealed record SavesPath(string Value)
{
    public const string Variable = "NPCFORGE_CHARACTERS";

    // A missing or blank value refuses to start and names the variable, so a run cannot fall back to
    // a file nobody chose and save over the owner's characters. That is the API's own rule, since the
    // console has a default file and the API has none. Whether the path is a full one is
    // FileCharacterStore's rule, which Program.cs meets when it builds the store from this value: under
    // the AppHost, DCP starts the API in its own project folder, so a relative path would name a file the
    // caller never meant, and the store refuses it however the API starts.
    public static SavesPath From(IConfiguration configuration)
    {
        var value = configuration[Variable];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{Variable} is not set. The API reads and writes saves in the file it names, and never in a default one. "
                + "The AppHost sets it. Outside the AppHost, set it to a saves file.");
        return new SavesPath(value);
    }
}
