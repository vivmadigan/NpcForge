namespace NpcForge;

// One person's saved characters. A store instance belongs to one GM: the console's file is the owner's,
// and the website builds one per logged-in friend, so nothing in the core names an owner. It is a port
// because saves move from a JSON file to a database without the core changing, and the core may not
// touch the file (ADR-014, decision 2). Async from the start, because a database is, and changing a
// port from sync to async later reaches every caller. The store numbers the saves, because a database
// numbers its own rows; CharacterStoreTests holds the rule for every store.
public interface ICharacterStore
{
    // Keeps the character under a number greater than every number the store holds, 1 when empty, and
    // never under a number a save holds. A store need not give out a removed number again.
    Task<int> SaveAsync(SavedCharacter character, CancellationToken ct);

    // The character saved under this number, its brief exactly as saved; null when none was.
    Task<SavedCharacter?> LoadAsync(int number, CancellationToken ct);

    // Every save's number, name, occupation and setting, in number order. Never the text.
    Task<IReadOnlyList<ListedCharacter>> ListAsync(CancellationToken ct);
}
