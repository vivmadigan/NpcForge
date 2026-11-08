using ModelContextProtocol;            // McpException
using System.Text.Encodings.Web;       // JavaScriptEncoder
using System.Text.Json;

namespace NpcForge.Server
{
    // All saved characters in one JSON file, numbered from 1. Only the server touches the file.
    public static class Storage
    {
        // Always absolute: a relative path depends on where the app was started (step 7). Tests
        // set NPCFORGE_CHARACTERS to a temp file; otherwise it sits in the server's project
        // folder, three up from bin\Debug\net10.0, where you can find it. The two-argument
        // GetFullPath, because a server run as `dotnet NpcForge.Server.dll` from a path of 260
        // characters or more gets AppContext.BaseDirectory as a \\?\ path, and the one-argument form
        // leaves ".." in such a path as it is (lesson 015). The console never meets this: Windows will
        // not start the server's exe from a path that long. The file APIs take a \\?\ path, so the
        // prefix can stay.
        private static string FilePath =>
            Environment.GetEnvironmentVariable("NPCFORGE_CHARACTERS")
            ?? Path.GetFullPath(Path.Combine("..", "..", "..", "characters.json"), AppContext.BaseDirectory);


        // Same camelCase names as the [app] brief line, indented so you can read the file, and
        // apostrophes and dashes left as they are.
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // All saved characters by number; empty if nothing is saved yet. If the file will not
        // parse, throw McpException with the path, so the app is told why.
        public static Dictionary<int, SavedCharacter> Load()
        {
            if (!File.Exists(FilePath))
                return new();

            try
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Dictionary<int, SavedCharacter>>(json, Options) ?? new();
            }
            catch (JsonException ex)
            {
                throw new McpException($"Could not read {FilePath}: {ex.Message}");
            }
        }


        // Writes them all back. The folder does not exist before the first save.
        public static void Save(Dictionary<int, SavedCharacter> all)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            var json = JsonSerializer.Serialize(all, Options);
            File.WriteAllText(FilePath, json);
        }
    }
}
