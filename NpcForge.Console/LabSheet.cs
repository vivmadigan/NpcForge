using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    // the GM reads only after guessing.
    public static (string Sheet, string Key) Blind(
        string briefJson, IReadOnlyList<LabEntry> entries, int planned, Random random)
    {
        var written = Shuffle(entries.Where(entry => entry.Text is not null), random);
        var letters = written.Select((entry, i) => ((char)('A' + i)).ToString()).ToList();

        var sheet = new StringBuilder();
        sheet.AppendLine("# Blind lab").AppendLine();
        sheet.AppendLine("## The brief, the same for every character").AppendLine();
        sheet.AppendLine("```json").AppendLine(briefJson).AppendLine("```").AppendLine();

        for (var i = 0; i < written.Count; i++)
            sheet.AppendLine($"## Character {letters[i]}").AppendLine().AppendLine(written[i].Text).AppendLine();

        var failed = entries.Count(entry => entry.Text is null);
        if (failed > 0)
            sheet.AppendLine(FailedNote(failed)).AppendLine();
        if (entries.Count < planned)
            sheet.AppendLine(StoppedNote(entries.Count, planned));

        var key = new
        {
            key = letters.Zip(written, (letter, entry) => (letter, entry.Label))
                .ToDictionary(pair => pair.letter, pair => pair.Label),
            failures = entries.Where(entry => entry.Text is null)
                .Select(entry => new { label = entry.Label, take = entry.Take, reason = entry.Failure }),
        };
        return (sheet.ToString(), JsonSerializer.Serialize(key, new JsonSerializerOptions { WriteIndented = true }));
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

    // The blind sheet with each letter's label and the guess beside it. A guess is right when it
    // equals the label, ignoring case. A letter with no guess counts as wrong.
    public static (int Right, int Total, string Scored) Score(
        string blindSheet, IReadOnlyDictionary<string, string> key, IReadOnlyDictionary<string, string> guesses)
    {
        var right = 0;
        var scored = Regex.Replace(blindSheet, @"^## Character ([A-Z])\r?$", match =>
        {
            var letter = match.Groups[1].Value;
            var label = key.GetValueOrDefault(letter, "(not in the key)");
            var guess = guesses.GetValueOrDefault(letter, "");
            var isRight = guess.Trim().Equals(label, StringComparison.OrdinalIgnoreCase);
            if (isRight)
                right++;
            return $"{match.Value}\n\nLabel: {label}. Your guess: {(guess == "" ? "none" : guess)}. {(isRight ? "Right." : "Wrong.")}";
        }, RegexOptions.Multiline);

        return (right, key.Count, scored);
    }

    private static string StoppedNote(int done, int planned) =>
        $"(stopped at the spending cap after {done} of {planned})";
}
