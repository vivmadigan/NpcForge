using System.Text.Json;
using System.Text.Json.Nodes;

namespace NpcForge.Tests;

// The save, load and list rules, written once and run against every ICharacterStore (ADR-014,
// decision 9; ROADMAP.md epic 5, "Done when"). Each store is a small subclass below, so xUnit runs every
// test here once per store, and a database store in epic 6 is one more subclass. The tests say what a
// caller sees, not how a store keeps it.
public abstract class CharacterStoreTests
{
    // A fresh, empty store for each test (xUnit makes a new instance per test).
    protected abstract ICharacterStore Store { get; }

    // What a hand edit does to the file: the port has no delete, and the store must still number
    // correctly after one.
    protected abstract void Remove(int number);

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task An_empty_store_lists_nothing_and_loads_null()
    {
        Assert.Empty(await Store.ListAsync(Ct));
        Assert.Null(await Store.LoadAsync(1, Ct));
    }

    [Fact]
    public async Task Saves_are_numbered_1_2_3()
    {
        Assert.Equal(1, await Store.SaveAsync(Saved("one"), Ct));
        Assert.Equal(2, await Store.SaveAsync(Saved("two"), Ct));
        Assert.Equal(3, await Store.SaveAsync(Saved("three"), Ct));
    }

    // Count + 1 would give 3 here and overwrite the save that holds that number. The promise is a number
    // greater than every one held, not 4 exactly: a store may number past it. Whether a removed highest
    // number is given again is left to each store, so no test says which (ADR-015).
    [Fact]
    public async Task After_a_gap_the_next_number_is_greater_than_every_number_held_and_no_save_is_overwritten()
    {
        await Store.SaveAsync(Saved("one"), Ct);
        await Store.SaveAsync(Saved("two"), Ct);
        await Store.SaveAsync(Saved("three"), Ct);
        Remove(2);

        var number = await Store.SaveAsync(Saved("four"), Ct);

        Assert.True(number > 3, $"saved under {number}, which is not greater than every number held (1 and 3)");
        Assert.Equal("three", (await Store.LoadAsync(3, Ct))!.Text);
        Assert.Equal("four", (await Store.LoadAsync(number, Ct))!.Text);
        Assert.Null(await Store.LoadAsync(2, Ct));
    }

    [Fact]
    public async Task Load_gives_back_the_brief_the_name_and_the_text_as_saved()
    {
        var brief = Brief("""{"setting":"the Gull's Rest","playersWant":null,"occupation":"ferryman","look":"a scar – café 😀"}""");
        var number = await Store.SaveAsync(new SavedCharacter(brief, new CharacterName(["Tam", "sin"], "Brook"), "A tired ferryman."), Ct);

        var loaded = await Store.LoadAsync(number, Ct);

        Assert.NotNull(loaded);
        Assert.Equal(Compact(brief), Compact(loaded.Brief));
        Assert.Equal(["Tam", "sin"], loaded.Name!.Syllables);
        Assert.Equal("Brook", loaded.Name.Family);
        Assert.Equal("A tired ferryman.", loaded.Text);
    }

    [Fact]
    public async Task A_save_with_no_name_loads_with_a_null_name()
    {
        var number = await Store.SaveAsync(new SavedCharacter(Brief("""{"setting":"an inn"}"""), null, "Old."), Ct);

        var loaded = await Store.LoadAsync(number, Ct);

        Assert.Null(loaded!.Name);
        Assert.Equal("Old.", loaded.Text);
    }

    [Fact]
    public async Task A_number_never_saved_loads_null()
    {
        await Store.SaveAsync(Saved("one"), Ct);

        Assert.Null(await Store.LoadAsync(2, Ct));
        Assert.Null(await Store.LoadAsync(0, Ct));
    }

    // Ten saves, so a list sorted as text (1, 10, 2, ...) fails.
    [Fact]
    public async Task List_is_in_number_order_with_each_name_occupation_and_setting()
    {
        for (var i = 1; i <= 10; i++)
            await Store.SaveAsync(new SavedCharacter(
                Brief($$"""{"setting":"setting {{i}}","occupation":"occupation {{i}}","tone":"Serious"}"""),
                new CharacterName([$"Name{i}"], "Family"),
                $"text {i}"), Ct);

        var rows = await Store.ListAsync(Ct);

        Assert.Equal(Enumerable.Range(1, 10), rows.Select(row => row.Number));
        Assert.All(rows, row =>
        {
            Assert.Equal($"occupation {row.Number}", row.Occupation);
            Assert.Equal($"setting {row.Number}", row.Setting);
            Assert.Equal([$"Name{row.Number}"], row.Name!.Syllables);
        });
    }

    // The 16 saves from before the namer have no name, and one may lack a key: a gap, not an invented word.
    [Fact]
    public async Task List_gives_null_for_a_brief_missing_the_occupation_or_the_setting_and_for_no_name()
    {
        await Store.SaveAsync(new SavedCharacter(Brief("""{"setting":"a river barge"}"""), null, "text"), Ct);
        await Store.SaveAsync(new SavedCharacter(Brief("""{"occupation":"goblin baker"}"""), null, "text"), Ct);

        var rows = await Store.ListAsync(Ct);

        Assert.Equal((null, "a river barge"), (rows[0].Occupation, rows[0].Setting));
        Assert.Equal(("goblin baker", null), (rows[1].Occupation, rows[1].Setting));
        Assert.All(rows, row => Assert.Null(row.Name));
    }

    // ADR-009: an old brief is the JSON it was saved as, so a field added to the brief later never shows up in it.
    [Fact]
    public async Task An_old_brief_comes_back_without_the_fields_added_since()
    {
        var old = Brief("""{"setting":"an inn","playersWant":"a name","difficulty":"Wall","occupation":"innkeeper","wrongAbout":"thinks they lie well"}""");
        var number = await Store.SaveAsync(new SavedCharacter(old, null, "Old."), Ct);

        var loaded = (await Store.LoadAsync(number, Ct))!;

        Assert.Equal(Compact(old), Compact(loaded.Brief));
        Assert.All(["tone", "race", "nameLetter", "gender", "age", "look"], newer =>
            Assert.False(loaded.Brief.TryGetProperty(newer, out _), $"{newer} appeared in an old brief"));
    }

    private static SavedCharacter Saved(string text) =>
        new(Brief("""{"setting":"an inn","occupation":"innkeeper"}"""), new CharacterName(["Tam"], "Reed"), text);

    private static JsonElement Brief(string json) => JsonDocument.Parse(json).RootElement.Clone();

    // The file indents what it keeps, so a loaded brief's raw text differs from the saved one by white
    // space alone. Compared compact, the names, the values and their order must still be the same.
    private static string Compact(JsonElement brief) => JsonNode.Parse(brief.GetRawText())!.ToJsonString();
}

public sealed class FileCharacterStoreTests : CharacterStoreTests, IDisposable
{
    // A temp file, never NpcForge.Console/characters.json, and a path handed to the store, so no
    // NPCFORGE_CHARACTERS is set.
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"npcforge-store-{Guid.NewGuid():N}.json");

    public FileCharacterStoreTests() => Store = new FileCharacterStore(_path);

    protected override ICharacterStore Store { get; }

    // A hand edit, as the owner would make one: the entry's key taken out of the file, which nothing in
    // the store is told about.
    protected override void Remove(int number)
    {
        var all = JsonNode.Parse(File.ReadAllText(_path))!.AsObject();
        all.Remove(number.ToString());
        File.WriteAllText(_path, all.ToJsonString());
    }

    public void Dispose() => File.Delete(_path);
}

public sealed class InMemoryCharacterStoreTests : CharacterStoreTests
{
    private readonly InMemoryCharacterStore _store = new();

    protected override ICharacterStore Store => _store;

    protected override void Remove(int number) => _store.Remove(number);
}
