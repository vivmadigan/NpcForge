using NpcForge.Server;

namespace NpcForge.Tests;

// The owner's save file, guarded through the port. StorageTests holds the same promises today by calling
// Storage and CharacterTools, which the behavior PR deletes with NpcForge.Server (ADR-014, decision 9), and
// until then it stays as it is. This file is the guard that replaces StorageTests when the server goes
// (ADR-014, ADR-015): it drives FileCharacterStore by a path handed to it, with no NPCFORGE_CHARACTERS, so
// it still holds once the store is the console's. ADR-009: an old entry is written back as the JSON it was
// saved as, so a field added to the brief later never shows up in it. It runs on a temp copy of the real
// characters.json, never the original.
public class SaveFileGuardTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static string RealSaves =>
        Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Server", "characters.json");

    [Fact]
    public async Task Saving_a_new_character_through_the_store_leaves_every_old_entry_byte_for_byte()
    {
        var copy = TempFile();
        try
        {
            File.Copy(RealSaves, copy);
            var before = File.ReadAllText(copy);
            var store = new FileCharacterStore(copy);
            var oldCount = (await store.ListAsync(Ct)).Count;

            var number = await store.SaveAsync(NewCharacter("A new character."), Ct);

            Assert.Equal(oldCount + 1, number);
            // The old file ends "  }", a line break, "}". The new one is that text with the closing
            // brace moved past the new entry, so everything before it must be unchanged. The break
            // is the writer's own, "\r\n" on Windows, so the test cuts at the brace, not at "\n".
            Assert.StartsWith(OldEntries(before) + "," + Environment.NewLine, File.ReadAllText(copy));
        }
        finally
        {
            File.Delete(copy);
        }
    }

    // ADR-009: an old save loads with an "unknown" line on screen only. The next save must leave that
    // entry as it was: no tone, no "unknown", not one byte different.
    [Fact]
    public async Task Saving_after_an_old_entry_leaves_its_json_byte_for_byte_with_no_unknown_in_the_file()
    {
        var file = TempFile();
        try
        {
            File.WriteAllText(file, """
                { "1": { "brief": { "setting": "an inn", "playersWant": "a name", "difficulty": "Wall", "occupation": "innkeeper",
                  "characterWants": "to be left alone", "obstacle": "Cant", "attitude": "distrustful", "mannerism": "fidgets",
                  "weakness": "flattery", "willNotDiscuss": "their age", "wrongAbout": "thinks they lie well" }, "text": "Old." } }
                """);
            var store = new FileCharacterStore(file);
            await store.SaveAsync(NewCharacter("First new."), Ct);   // the file as the store writes it
            var before = File.ReadAllText(file);

            await store.SaveAsync(NewCharacter("Second new."), Ct);

            var after = File.ReadAllText(file);
            var oldEntries = OldEntries(before);
            Assert.StartsWith(oldEntries + "," + Environment.NewLine, after);
            Assert.DoesNotContain("unknown", after);
            // Entry 1 ends where the first new entry's key begins.
            var entryOne = oldEntries[..oldEntries.IndexOf("\"2\":", StringComparison.Ordinal)];
            Assert.Contains("\"wrongAbout\"", entryOne);
            Assert.DoesNotContain("null", entryOne);             // no "tone": null written into the old entry
            Assert.DoesNotContain("\"tone\"", entryOne);
            Assert.DoesNotContain("\"name\"", entryOne);         // and no name: an old save never had one
            // The new entry keeps its name, split as the namer split it.
            Assert.Contains("\"name\": {", after[oldEntries.Length..]);
            Assert.Contains("\"Tam\"", after[oldEntries.Length..]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // Everything before the file's closing brace and the line break in front of it.
    private static string OldEntries(string file) => file[..file.LastIndexOf('}')].TrimEnd();

    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"npcforge-saveguard-{Guid.NewGuid():N}.json");

    private static SavedCharacter NewCharacter(string text) => SavedCharacter.Of(new CharacterBrief
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
        Gender = Gender.Male,
        Age = Age.MiddleAged,
        Look = "a scar across one cheek",
    }, new CharacterName(["Tam", "sin"], "Brook"), text);
}
