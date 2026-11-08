namespace NpcForge.Server
{
    // The saves as one JSON file, numbered from 1: the store the console and the owner use today. It
    // holds the rules a store keeps (ICharacterStore) and leaves the file to Storage. The Tasks are
    // already complete when they are returned, because the file is read and written whole in-process.
    // A database store awaits for real, so the port is async from the start (ADR-014, decision 2).
    public sealed class FileCharacterStore(string path) : ICharacterStore
    {
        public Task<int> SaveAsync(SavedCharacter character, CancellationToken ct)
        {
            var all = Storage.Load(path);

            // One past the highest, not Count + 1: after a hand edit that deletes an entry,
            // Count + 1 could save over the last one.
            var number = all.Count == 0 ? 1 : all.Keys.Max() + 1;

            all[number] = character;
            Storage.Save(path, all);
            return Task.FromResult(number);
        }

        // The save as the file holds it: an old brief is never passed through CharacterBrief (ADR-009).
        public Task<SavedCharacter?> LoadAsync(int number, CancellationToken ct) =>
            Task.FromResult(Storage.Load(path).GetValueOrDefault(number));

        public Task<IReadOnlyList<ListedCharacter>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ListedCharacter>>(
                [.. Storage.Load(path).OrderBy(entry => entry.Key).Select(entry => ListedCharacter.Of(entry.Key, entry.Value))]);
    }
}
