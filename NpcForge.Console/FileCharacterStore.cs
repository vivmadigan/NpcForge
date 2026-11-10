using System.Text.Encodings.Web;       // JavaScriptEncoder
using System.Text.Json;
using System.Text.Json.Serialization;  // JsonIgnoreCondition

namespace NpcForge;

// The saves as one JSON file, numbered from 1: the store the console and the owner use. Only this
// class reads or writes the file. The Tasks are already complete when they are returned, because the
// file is read and written whole in-process. A database store awaits for real, so the port is async
// from the start (ADR-014, decision 2).
public sealed class FileCharacterStore(string path) : ICharacterStore
{
    // The owner's saves when nothing names another file: NpcForge.Console/characters.json, three folders
    // up from bin\Debug\net10.0, where you can find it. Always absolute: a relative path depends on where
    // the app was started (step 7). The two-argument GetFullPath, because a console run as
    // `dotnet NpcForge.Console.dll` from a path of 260 characters or more gets AppContext.BaseDirectory as
    // a \\?\ path, and the one-argument form leaves ".." in such a path as it is (lesson 015,
    // LongFolderTests). The file APIs take a \\?\ path, so the prefix can stay.
    public static string DefaultPath { get; } =
        Path.GetFullPath(Path.Combine("..", "..", "..", "characters.json"), AppContext.BaseDirectory);

    // Same camelCase names as the [app] brief line, indented so you can read the file, and apostrophes
    // and dashes left as they are. A save with no name leaves the key out, so an old entry keeps its bytes
    // when the file is written back. That is the file's rule, so it lives here and not on SavedCharacter
    // (ADR-015, decision 5). A brief is a JsonElement and is written as it is, so a null inside it, a want
    // not given, stays in the file.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Task<int> SaveAsync(SavedCharacter character, CancellationToken ct)
    {
        var all = ReadAll();

        // The port promises a number greater than every number held. One past the highest keeps
        // it; Count + 1 would not, because after a hand edit that deletes an entry it could save
        // over the last one.
        var number = all.Count == 0 ? 1 : all.Keys.Max() + 1;

        all[number] = character;
        WriteAll(all);
        return Task.FromResult(number);
    }

    // The save as the file holds it: an old brief is never passed through CharacterBrief (ADR-009).
    public Task<SavedCharacter?> LoadAsync(int number, CancellationToken ct) =>
        Task.FromResult(ReadAll().GetValueOrDefault(number));

    public Task<IReadOnlyList<ListedCharacter>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ListedCharacter>>(
            [.. ReadAll().OrderBy(entry => entry.Key).Select(entry => ListedCharacter.Of(entry.Key, entry.Value))]);

    // All saved characters by number; empty if nothing is saved yet. A file that will not parse throws
    // InvalidDataException with the path, and the console reports it as one line (ADR-015, decision 4).
    private Dictionary<int, SavedCharacter> ReadAll()
    {
        if (!File.Exists(path))
            return new();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<int, SavedCharacter>>(File.ReadAllText(path), Options) ?? new();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Could not read {path}: {ex.Message}", ex);
        }
    }

    // Writes them all back. The folder does not exist before the first save.
    private void WriteAll(Dictionary<int, SavedCharacter> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(all, Options));
    }
}
