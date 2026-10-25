using NpcForge.Server;

namespace NpcForge.Tests;

// The save file is the owner's. ADR-009 decision 2: an old entry is read and written back as the
// JSON it was saved as, so a brief field added later never shows up in it. These tests call
// Storage itself, the one place that reads and writes the file, and use a copy of the real
// characters.json at a temp path, never the original.
// Storage reads NPCFORGE_CHARACTERS on every call, so the tests set it for the process and put
// it back. They share one collection so they do not overlap each other.
[Collection("Storage")]
public class StorageTests
{
    private static string RealSaves =>
        Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Server", "characters.json");

    [Fact]
    public void The_real_saves_round_trip_through_load_and_save_byte_for_byte()
    {
        WithCopyOfRealSaves(copy =>
        {
            var before = File.ReadAllBytes(copy);

            Storage.Save(Storage.Load());

            Assert.Equal(before, File.ReadAllBytes(copy));
        });
    }

    [Fact]
    public void Saving_a_new_character_leaves_every_old_entry_byte_for_byte()
    {
        WithCopyOfRealSaves(copy =>
        {
            var before = File.ReadAllText(copy);
            var oldCount = Storage.Load().Count;

            // Through the tool, as the app saves: a typed brief in, the file written whole.
            var number = CharacterTools.SaveCharacter(NewBrief(), "A new character.");

            var after = File.ReadAllText(copy);
            Assert.Equal(oldCount + 1, number);

            // The old file ends "  }\n}". The new one is that text with the closing brace moved
            // past the new entry, so everything before it must be unchanged.
            var oldEntries = before[..before.LastIndexOf("\n}", StringComparison.Ordinal)];
            Assert.StartsWith(oldEntries + ",\n", after);
        });
    }

    private static void WithCopyOfRealSaves(Action<string> test)
    {
        var copy = Path.Combine(Path.GetTempPath(), $"npcforge-storage-{Guid.NewGuid():N}.json");
        var told = Environment.GetEnvironmentVariable("NPCFORGE_CHARACTERS");
        try
        {
            File.Copy(RealSaves, copy);
            Environment.SetEnvironmentVariable("NPCFORGE_CHARACTERS", copy);
            test(copy);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NPCFORGE_CHARACTERS", told);
            File.Delete(copy);
        }
    }

    private static CharacterBrief NewBrief() => new()
    {
        Setting = "a harbour town",
        PlayersWant = "a boat",
        Difficulty = Difficulty.Easy,
        Occupation = "ferryman",
        CharacterWants = "to be left alone",
        Obstacle = ObstacleKind.Wont,
        Attitude = "tired",
        Mannerism = "hums",
        Weakness = "gambling",
        WillNotDiscuss = "his brother",
        WrongAbout = "the tides",
    };
}
