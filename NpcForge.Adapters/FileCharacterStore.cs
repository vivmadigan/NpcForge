using System.Diagnostics;              // Stopwatch
using System.Text;
using System.Text.Encodings.Web;       // JavaScriptEncoder
using System.Text.Json;
using System.Text.Json.Serialization;  // JsonIgnoreCondition

namespace NpcForge;

// The saves as one JSON file, numbered from 1: the store the console and the owner use. Only this
// class reads or writes the file. The file is read and written whole, so a Task is complete when it is
// returned unless another handle holds the file, and then it waits. The console and the API each hold one
// store, so the lock is the operating system's, on the file, and it holds between the two processes
// (ADR-019, decision 5). A database store awaits for real, so the port is async from the start
// (ADR-014, decision 2).
public sealed class FileCharacterStore(string path) : ICharacterStore
{
    // The owner's saves when nothing names another file: NpcForge.Console/characters.json, three folders
    // up from bin\Debug\net10.0, where you can find it. AppContext.BaseDirectory is the folder of the
    // program that is running, not of this assembly, so this finds the console's file only when the
    // console runs: it is the console's rule, and the website's API never reads it (ADR-019, decision 1).
    // Always absolute: a relative path depends on where the app was started (step 7). The two-argument
    // GetFullPath, because a console run as `dotnet NpcForge.Console.dll` from a path of 260 characters
    // or more gets AppContext.BaseDirectory as a \\?\ path, and the one-argument form leaves ".." in such
    // a path as it is (lesson 015, LongFolderTests). The file APIs take a \\?\ path, so the prefix can stay.
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

    // The one handle a save keeps open across the read, the number and the write, with no other handle let
    // in: two saves, or a save and a list, never meet in the file. Without it two stores that both read 16
    // entries both take 17, and one character is lost (ADR-019, decision 5).
    public async Task<int> SaveAsync(SavedCharacter character, CancellationToken ct)
    {
        // The folder does not exist before the first save.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = await OpenAsync(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, ct);
        var all = ReadAll(file);

        // The port promises a number greater than every number held. One past the highest keeps
        // it; Count + 1 would not, because after a hand edit that deletes an entry it could save
        // over the last one.
        var number = all.Count == 0 ? 1 : all.Keys.Max() + 1;

        all[number] = character;

        // The whole file again, through the handle that read it. The bytes are File.WriteAllText's: UTF-8
        // with no byte order mark, so an old entry keeps its bytes (SaveFileGuardTests). They go over the
        // old bytes, and the file is cut to their length only after them. So a process that stops part way
        // through a save leaves the old file or one that will not parse, which the next list reports. It
        // never leaves an empty file where saves were, which would read as no saves and let the next save
        // start again at 1.
        file.Position = 0;
        file.Write(new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(all, Options)));
        file.SetLength(file.Position);
        return number;
    }

    // The save as the file holds it: an old brief is never passed through CharacterBrief (ADR-009).
    public async Task<SavedCharacter?> LoadAsync(int number, CancellationToken ct) =>
        (await ReadSharedAsync(ct)).GetValueOrDefault(number);

    public async Task<IReadOnlyList<ListedCharacter>> ListAsync(CancellationToken ct) =>
        [.. (await ReadSharedAsync(ct)).OrderBy(entry => entry.Key).Select(entry => ListedCharacter.Of(entry.Key, entry.Value))];

    // A list or a load shares the file with other readers and nobody else, so it waits for a save that is
    // writing. A file or a folder that is not there holds no saves.
    private async Task<Dictionary<int, SavedCharacter>> ReadSharedAsync(CancellationToken ct)
    {
        try
        {
            using var file = await OpenAsync(FileMode.Open, FileAccess.Read, FileShare.Read, ct);
            return ReadAll(file);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new();
        }
    }

    // The one retry rule. An IOException that is not a missing file or folder most often means another
    // handle holds the file: a save or a list, in this process or in the other front end. Wait and try
    // again until HeldFor has gone, then let that exception through as it is, since its message names the
    // file. Any other IOException, such as a path too long, waits the same HeldFor before it is thrown.
    // Only the open is retried, never a read or a write. The type of the exception decides, not a Windows
    // error code, so the rule holds where the lock is a Unix one.
    private async Task<FileStream> OpenAsync(FileMode mode, FileAccess access, FileShare share, CancellationToken ct)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(path, mode, access, share);
            }
            catch (IOException ex) when (ex is not (FileNotFoundException or DirectoryNotFoundException)
                                         && waited.Elapsed < HeldFor)
            {
                await Task.Delay(RetryEvery, ct);
            }
        }
    }

    private static readonly TimeSpan RetryEvery = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan HeldFor = TimeSpan.FromSeconds(5);

    // All saved characters by number, read from the handle the caller holds. A zero-byte file holds no
    // saves, for a reader and a writer alike: the first save's OpenOrCreate makes an empty file and then
    // reads it, and a first save that stops before it writes leaves one behind. A file with bytes that will
    // not parse throws InvalidDataException with the path, and the console reports it as one line
    // (ADR-015, decision 4).
    private Dictionary<int, SavedCharacter> ReadAll(FileStream file)
    {
        if (file.Length == 0)
            return new();

        Dictionary<int, SavedCharacter> all;
        try
        {
            // UTF-8, and a byte order mark that says otherwise wins: what File.ReadAllText does.
            using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            all = JsonSerializer.Deserialize<Dictionary<int, SavedCharacter>>(reader.ReadToEnd(), Options) ?? new();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Could not read {path}: {ex.Message}", ex);
        }

        // The JSON parses, but a hand edit left an entry that is not a save: the brief or the text is
        // gone. The serializer fills the gap with a default it cannot write back, so the entry is
        // refused here, by its number, and not later as a crash in whoever reads it.
        foreach (var (number, saved) in all)
            if (saved is null || saved.Brief.ValueKind != JsonValueKind.Object || saved.Text is null)
                throw new InvalidDataException($"Could not read {path}: entry {number} has no brief or no text.");

        return all;
    }
}
