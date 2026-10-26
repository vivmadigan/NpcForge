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
            var number = CharacterTools.SaveCharacter(NewBrief(), NewName, "A new character.");

            var after = File.ReadAllText(copy);
            Assert.Equal(oldCount + 1, number);

            // The old file ends "  }", a line break, "}". The new one is that text with the closing
            // brace moved past the new entry, so everything before it must be unchanged. The break
            // is the writer's own, "\r\n" on Windows, so the test cuts at the brace, not at "\n".
            var oldEntries = OldEntries(before);
            Assert.StartsWith(oldEntries + "," + Environment.NewLine, after);
        });
    }

    // ADR-009: an old save loads with an "unknown" line on screen only. The next save must leave that
    // entry as it was: no tone, no "unknown", not one byte different.
    [Fact]
    public void Saving_after_an_old_entry_leaves_its_json_byte_for_byte_with_no_unknown_in_the_file()
    {
        var file = Path.Combine(Path.GetTempPath(), $"npcforge-storage-{Guid.NewGuid():N}.json");
        var told = Environment.GetEnvironmentVariable("NPCFORGE_CHARACTERS");
        try
        {
            Environment.SetEnvironmentVariable("NPCFORGE_CHARACTERS", file);
            File.WriteAllText(file, """
                { "1": { "brief": { "setting": "an inn", "playersWant": "a name", "difficulty": "Wall", "occupation": "innkeeper",
                  "characterWants": "to be left alone", "obstacle": "Cant", "attitude": "distrustful", "mannerism": "fidgets",
                  "weakness": "flattery", "willNotDiscuss": "their age", "wrongAbout": "thinks they lie well" }, "text": "Old." } }
                """);
            Storage.Save(Storage.Load());                    // the file as Storage writes it
            var before = File.ReadAllText(file);

            CharacterTools.SaveCharacter(NewBrief(), NewName, "New.");

            var after = File.ReadAllText(file);
            var oldEntry = OldEntries(before);
            Assert.StartsWith(oldEntry + "," + Environment.NewLine, after);
            Assert.DoesNotContain("unknown", after);
            Assert.DoesNotContain("null", oldEntry);         // no "tone": null written into the old entry
            Assert.DoesNotContain("\"name\"", oldEntry);     // and no name: an old save never had one
            // The new entry keeps its name, split as the namer split it.
            Assert.Contains("\"name\": {", after[oldEntry.Length..]);
            Assert.Contains("\"Tam\"", after[oldEntry.Length..]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NPCFORGE_CHARACTERS", told);
            File.Delete(file);
        }
    }

    // Everything before the file's closing brace and the line break in front of it.
    private static string OldEntries(string file) => file[..file.LastIndexOf('}')].TrimEnd();

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

    private static readonly CharacterName NewName = new(["Tam", "sin"], "Brook");

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
        Tone = Tone.Serious,
        Race = "human",
        NameLetter = NameLetter.T,
        NameSyllables = Syllables.Two,
        FamilyLetter = NameLetter.B,
    };
}
