namespace NpcForge.Api;

// The one file the API reads and writes saves in, parsed once where it enters from the outside
// (ADR-019, decision 5). Never FileCharacterStore.DefaultPath: that is three folders up from the
// running program, which is the owner's file only when the console runs. Here the AppHost, or an agent
// pointing a run at a scratch file, says which file it is.
public sealed record SavesPath(string Value)
{
    public const string Variable = "NPCFORGE_CHARACTERS";

    // A missing or blank value refuses to start and names the variable, so a run cannot fall back to
    // a file nobody chose and save over the owner's characters. So does a relative one. Under the
    // AppHost, DCP starts the API in its own project folder, so a relative path names a file the caller
    // never meant. A direct `dotnet run` would read it from the caller's folder, but one rule holds
    // however the API starts.
    public static SavesPath From(IConfiguration configuration)
    {
        var value = configuration[Variable];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{Variable} is not set. The API reads and writes saves in the file it names, and never in a default one. "
                + "The AppHost sets it. Outside the AppHost, set it to a saves file.");
        if (!Path.IsPathFullyQualified(value))
            throw new InvalidOperationException(
                $"{Variable} is \"{value}\", a relative path. Under the AppHost the API runs in its own project folder, "
                + "not yours, so it would name a different file. Set it to an absolute path.");
        return new SavesPath(value);
    }
}
