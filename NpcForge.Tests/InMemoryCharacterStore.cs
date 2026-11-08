namespace NpcForge.Tests;

// The second store behind ICharacterStore (ADR-014, decision 9): the same rules as the file, with no
// file, so CharacterStoreTests can run once against both and show that the rules belong to the port and
// not to the file. A test double, never shipped.
internal sealed class InMemoryCharacterStore : ICharacterStore
{
    private readonly Dictionary<int, SavedCharacter> _saves = [];

    public Task<int> SaveAsync(SavedCharacter character, CancellationToken ct)
    {
        // One past the highest, as the file store numbers, not Count + 1.
        var number = _saves.Count == 0 ? 1 : _saves.Keys.Max() + 1;
        _saves[number] = character;
        return Task.FromResult(number);
    }

    public Task<SavedCharacter?> LoadAsync(int number, CancellationToken ct) =>
        Task.FromResult(_saves.GetValueOrDefault(number));

    public Task<IReadOnlyList<ListedCharacter>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ListedCharacter>>(
            [.. _saves.OrderBy(entry => entry.Key).Select(entry => ListedCharacter.Of(entry.Key, entry.Value))]);

    // What a hand edit does to the file: the port has no delete, and a store must still number correctly after one.
    public void Remove(int number) => _saves.Remove(number);
}
