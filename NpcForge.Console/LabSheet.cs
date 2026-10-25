using System.Text;
using System.Text.Json;

namespace NpcForge;

// One character the lab tried to write. Skill is the [skill] line the writer was given, so a sheet
// shows exactly which text it was written from. Exactly one of Text and Failure is set.
public sealed record LabEntry(string Label, int Take, string Skill, string? Text, string? Failure);

// The lab's sheets: pure functions from entries to text, so they are tested with no model and no
// server. Lab.cs decides what is written; this decides how it reads.
public static class LabSheet
{
    // A blind sheet names its characters A to Z.
    public const int MostBlindCharacters = 26;

    // Fisher-Yates over a copy. The caller's Random decides the order, so a test that builds the
    // same Random from the same seed gets the same order.
    public static List<T> Shuffle<T>(IEnumerable<T> items, Random random)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    // The sheet for sitting down with: the brief once, then every character under its label and
    // take, with the skill it was given. A variant that failed stays, with its reason. `planned` is
    // how many the lab meant to write: fewer entries than that means the spending cap stopped it,
    // because every other failure is kept as an entry and the lab goes on.
    public static string SideBySide(string axis, string briefJson, IReadOnlyList<LabEntry> entries, int planned)
    {
        var sheet = new StringBuilder();
        sheet.AppendLine($"# Lab: {axis}").AppendLine();
        sheet.AppendLine("## The brief, the same for every character").AppendLine();
        sheet.AppendLine("```json").AppendLine(briefJson).AppendLine("```").AppendLine();

        foreach (var entry in entries)
        {
            sheet.AppendLine($"## {entry.Label}, take {entry.Take}").AppendLine();
            sheet.AppendLine($"`{entry.Skill}`").AppendLine();
            sheet.AppendLine(entry.Text ?? $"**Failed.** {entry.Failure}").AppendLine();
        }

        if (entries.Count < planned)
            sheet.AppendLine(StoppedNote(entries.Count, planned));
        return sheet.ToString();
    }

    // The sheet for judging without knowing: characters as A, B, C in a shuffled order, with no
    // label, no skill line and no failure text, and a key from letter to label. A failed variant is
    // left off the sheet, because its label or reason would give its letter away by elimination. It
    // is counted in a closing line, and written with its label, take and reason into key.json, which
    // the GM reads only after guessing. The third result is blind.json: the same characters as data,
    // so --score never has to find a letter by reading the model's prose.
    public static (string Sheet, string Key, string Data) Blind(
        string briefJson, IReadOnlyList<LabEntry> entries, int planned, Random random)
    {
        var written = Shuffle(entries.Where(entry => entry.Text is not null), random);
        var characters = written
            .Select((entry, i) => new BlindCharacter(((char)('A' + i)).ToString(), entry.Text!))
            .ToList();

        var notes = new List<string>();
        var failed = entries.Count(entry => entry.Text is null);
        if (failed > 0)
            notes.Add(FailedNote(failed));
        if (entries.Count < planned)
            notes.Add(StoppedNote(entries.Count, planned));
        var blind = new BlindSheet(briefJson, characters, notes);

        var key = new
        {
            key = characters.Zip(written, (character, entry) => (character.Letter, entry.Label))
                .ToDictionary(pair => pair.Letter, pair => pair.Label),
            failures = entries.Where(entry => entry.Text is null)
                .Select(entry => new { label = entry.Label, take = entry.Take, reason = entry.Failure }),
        };
        return (Render("Blind lab", blind, annotations: null),
            JsonSerializer.Serialize(key, Indented), JsonSerializer.Serialize(blind, Indented));
    }

    private static readonly JsonSerializerOptions Indented = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // What blind.json holds: the brief, the characters by letter in sheet order, and the closing
    // notes. No label, no skill line and no failure reason, the same as blind.md.
    private sealed record BlindCharacter(string Letter, string Text);
    private sealed record BlindSheet(string Brief, List<BlindCharacter> Characters, List<string> Notes);

    // One layout for the blind sheet and the scored one, so scored.md is blind.md plus a line under
    // each letter. `annotations` maps a letter to that line.
    private static string Render(string title, BlindSheet blind, IReadOnlyDictionary<string, string>? annotations)
    {
        var sheet = new StringBuilder();
        sheet.AppendLine($"# {title}").AppendLine();
        sheet.AppendLine("## The brief, the same for every character").AppendLine();
        sheet.AppendLine("```json").AppendLine(blind.Brief).AppendLine("```").AppendLine();

        foreach (var character in blind.Characters)
        {
            sheet.AppendLine($"## Character {character.Letter}").AppendLine();
            if (annotations?.GetValueOrDefault(character.Letter) is { } line)
                sheet.AppendLine(line).AppendLine();
            sheet.AppendLine(character.Text).AppendLine();
        }

        foreach (var note in blind.Notes)
            sheet.AppendLine(note).AppendLine();
        return sheet.ToString();
    }

    // The one sentence about failures that may appear before the GM has guessed: a count, and
    // where the reasons are. Lab.cs prints the same words on stderr.
    public static string FailedNote(int failed) =>
        $"{failed} {(failed == 1 ? "character" : "characters")} failed to write; the reasons are in key.json";

    // key.json holds the letters under "key" and the failures beside them. Only the letters are read
    // back for scoring.
    public static IReadOnlyDictionary<string, string> ReadKey(string keyJson)
    {
        using var document = JsonDocument.Parse(keyJson);
        return new SortedDictionary<string, string>(
            document.RootElement.GetProperty("key").Deserialize<Dictionary<string, string>>()
            ?? throw new ArgumentException("key.json holds no key."));
    }

    // The blind sheet with each letter's label and the guess beside it, drawn from blind.json and
    // never from blind.md: a character's own text may hold a line that looks like a heading. A
    // guess is right when it equals the label, ignoring case. A letter with no guess counts as wrong.
    public static (int Right, int Total, string Scored) Score(
        string blindJson, IReadOnlyDictionary<string, string> key, IReadOnlyDictionary<string, string> guesses)
    {
        var blind = JsonSerializer.Deserialize<BlindSheet>(blindJson, Indented)
            ?? throw new ArgumentException("blind.json holds no sheet.");

        var right = 0;
        var annotations = new Dictionary<string, string>();
        foreach (var character in blind.Characters)
        {
            var label = key.GetValueOrDefault(character.Letter, "(not in the key)");
            var guess = guesses.GetValueOrDefault(character.Letter, "");
            var isRight = guess.Trim().Equals(label, StringComparison.OrdinalIgnoreCase);
            if (isRight)
                right++;
            annotations[character.Letter] =
                $"Label: {label}. Your guess: {(guess == "" ? "none" : guess)}. {(isRight ? "Right." : "Wrong.")}";
        }

        return (right, key.Count, Render("Blind lab, scored", blind, annotations));
    }

    private static string StoppedNote(int done, int planned) =>
        $"(stopped at the spending cap after {done} of {planned})";
}
