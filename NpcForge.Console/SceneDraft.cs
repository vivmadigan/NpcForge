namespace NpcForge;

// Where a field's value came from (ADR-007). Code decides it from the field's kind, never the model:
// record_answer has no label argument. MyWords and Inferred belong to a recorded answer; Rolled and
// Default are what a field with no answer gets, decided by whether roll_character requires it.
public enum Label { MyWords, Inferred, Rolled, Default }

// Why record_answer refused a call, one reason per check, so a test can assert which check refused
// (lesson 004) and the model is told what to fix.
public enum Rejection
{
    None,
    UnknownField,
    EmptyValue,
    ValueNotInList,
    EmptyQuote,
    QuoteTooLong,
    QuoteNotTyped,
    ValueNotInQuote,
}

// One recorded field. Value and Quote are the GM's text as typed, cut from the scene (a choice's
// value is the server's spelling). Sentence is the whole sentence the quote sits in, shown at the
// confirmation so a fragment that drops a "doesn't" is visible beside it.
public sealed record Answer(string Value, string Quote, string Sentence, Label Label);

// The outcome of one record_answer call. Message goes back to the model as the tool's result.
public sealed record RecordResult(Rejection Reason, string Message)
{
    public bool Accepted => Reason == Rejection.None;
}

// One row of the confirmation: every field, in the server's order, recorded or not.
public sealed record ConfirmationLine(RollField Field, string? Value, Label Label, string? Quote, string? Sentence);

// What the interview has gathered from the GM's scene. Pure: no model, no server, no console, so
// every check has a free test. The interview only ever adds to it through Record.
public sealed class SceneDraft(string scene, IReadOnlyList<RollField> fields)
{
    // The scene's rule from 2026-09-26 (step 13 in `git show eddc444:ROADMAP.md`): a quote is one
    // sentence of at most 12 words.
    public const int MostWordsInAQuote = 12;

    private readonly IReadOnlyList<Words.Sentence> _sentences = Words.Sentences(scene);
    private readonly Dictionary<string, Answer> _answers = [];

    public IReadOnlyDictionary<string, Answer> Answers => _answers;

    // The only way in. Each check has its own reason, in this order. A rejection changes nothing, and
    // a second answer for a field replaces the first, so a model that retries or repeats itself
    // leaves the draft as one clean call would.
    public RecordResult Record(string field, string value, string quote)
    {
        var target = fields.FirstOrDefault(f => f.Name == field);
        if (target is null)
            return Reject(Rejection.UnknownField,
                $"there is no field named '{field}'. The fields are: {string.Join(", ", fields.Select(f => f.Name))}.");

        var valueWords = Words.Normalise(value);
        if (valueWords.Length == 0)
            return Reject(Rejection.EmptyValue, $"the value for {field} is empty.");

        // A choice is stored as the server spells it. Compared as letters only, so "Can't", "for a
        // price" and "Some Work" find Cant, ForAPrice and SomeWork.
        string? choice = null;
        if (target.Choices is not null)
        {
            choice = target.Choices.FirstOrDefault(c => Words.Squashed(c) == Words.Squashed(value));
            if (choice is null)
                return Reject(Rejection.ValueNotInList,
                    $"{field} must be one of: {string.Join(", ", target.Choices)}.");
        }

        var quoteWords = Words.Split(quote);
        if (quoteWords.Count == 0)
            return Reject(Rejection.EmptyQuote, $"the quote for {field} is empty. Quote the GM's words.");

        // A whole-scene quote would cite everything, so a quote is held to one sentence.
        if (quoteWords.Count > MostWordsInAQuote || Words.EndsASentenceBeforeTheLastWord(quote))
            return Reject(Rejection.QuoteTooLong,
                $"the quote for {field} must be one sentence of at most {MostWordsInAQuote} words. Quote only the words that say it.");

        var found = Find(quoteWords);
        if (found is null)
        {
            // Every word is there, but across the end of a sentence: still more than one sentence.
            return Words.IndexOf(_sentences.SelectMany(s => s.Words).ToList(), quoteWords) >= 0
                ? Reject(Rejection.QuoteTooLong,
                    $"the quote for {field} runs across two sentences of the scene. Quote within one sentence.")
                : Reject(Rejection.QuoteNotTyped,
                    $"the GM never typed \"{quote}\". Quote the GM's words exactly, or leave {field} to the dice.");
        }

        var (sentence, at) = found.Value;

        // The quote is kept as the GM typed it too, so the confirmation shows the GM's own words.
        var typedQuote = Typed(sentence, at, quoteWords.Count);

        if (choice is not null)
            return Accept(field, new Answer(choice, typedQuote, sentence.Text, Label.Inferred));

        // Free text: the value must be the GM's own words, a run of whole words inside the quote. A
        // value the model can only paraphrase is not recorded, so the dice roll it (ADR-007).
        var valueSplit = Words.Split(value);
        var offset = Words.IndexOf(quoteWords, valueSplit);
        if (offset < 0)
            return Reject(Rejection.ValueNotInQuote,
                $"\"{value}\" is not the GM's words in \"{quote}\". For {field}, the value must be words copied from the quote. Leave it to the dice if the GM did not say it.");

        // Stored as the GM typed it, from the scene, not as the model spelled it.
        return Accept(field, new Answer(Typed(sentence, at + offset, valueSplit.Count), typedQuote, sentence.Text, Label.MyWords));
    }

    // A run of the scene's words, cut from the scene itself: the GM's case and punctuation.
    private string Typed(Words.Sentence sentence, int at, int count) =>
        scene[sentence.Words[at].Start..sentence.Words[at + count - 1].End];

    // Every field in the server's order. An answer keeps its own label; a required field nobody
    // recorded takes its flag default; anything else is left to the dice.
    public IReadOnlyList<ConfirmationLine> Lines(IReadOnlyDictionary<string, object?> defaults) =>
    [
        .. fields.Select(field => _answers.TryGetValue(field.Name, out var answer)
            ? new ConfirmationLine(field, answer.Value, answer.Label, answer.Quote, answer.Sentence)
            : field.Required
                ? new ConfirmationLine(field, DefaultFor(field, defaults), Label.Default, null, null)
                : new ConfirmationLine(field, null, Label.Rolled, null, null)),
    ];

    // roll_character's arguments. A rolled field is left out, so the server's dice pick it.
    public Dictionary<string, object?> RollArguments(IReadOnlyDictionary<string, object?> defaults) =>
        Lines(defaults)
            .Where(line => line.Label != Label.Rolled)
            .ToDictionary(line => line.Field.Name, line => (object?)line.Value);

    // A required field the flag table does not know: the server grew a parameter RunOptions lacks.
    private static string DefaultFor(RollField field, IReadOnlyDictionary<string, object?> defaults) =>
        defaults.TryGetValue(field.Name, out var value) && value is not null
            ? value.ToString()!
            : throw new InvalidOperationException($"roll_character requires '{field.Name}' and there is no default for it.");

    // The first sentence of the scene that holds the quote as whole words, and where in it.
    private (Words.Sentence Sentence, int At)? Find(IReadOnlyList<Words.Word> quote)
    {
        foreach (var sentence in _sentences)
        {
            var at = Words.IndexOf(sentence.Words, quote);
            if (at >= 0)
                return (sentence, at);
        }
        return null;
    }

    private RecordResult Accept(string field, Answer answer)
    {
        _answers[field] = answer;
        return new RecordResult(Rejection.None,
            $"Recorded {field} = \"{answer.Value}\" ({(answer.Label == Label.MyWords ? "the GM's words" : "inferred")}).");
    }

    private static RecordResult Reject(Rejection reason, string why) => new(reason, $"Rejected: {why}");
}

// The one normalising function for the scene, the quote and the value (decided 2026-09-26, step 13
// in `git show eddc444:ROADMAP.md`). Everything that compares words goes through Split, so the
// three can never be normalised two different ways.
public static class Words
{
    // A word of the original text: Text is normalised, Start and End index the original, so a
    // match can be handed back as the GM typed it.
    public sealed record Word(string Text, int Start, int End);

    // One sentence of the scene: its words and its original text, trimmed.
    public sealed record Sentence(string Text, IReadOnlyList<Word> Words);

    // Lowercase. A word is letters and digits; an apostrophe between two of them stays in the word,
    // curly or straight, written straight. Everything else, full stops and quote marks included,
    // separates words. So "Doesn’t  want." and "doesn't want" are the same two words.
    public static IReadOnlyList<Word> Split(string text)
    {
        var words = new List<Word>();
        var i = 0;
        while (i < text.Length)
        {
            if (!char.IsLetterOrDigit(text[i]))
            {
                i++;
                continue;
            }

            var start = i;
            var normal = new System.Text.StringBuilder();
            while (i < text.Length)
            {
                if (char.IsLetterOrDigit(text[i]))
                    normal.Append(char.ToLowerInvariant(text[i]));
                else if (IsApostrophe(text[i]) && i + 1 < text.Length && char.IsLetterOrDigit(text[i + 1]))
                    normal.Append('\'');
                else
                    break;
                i++;
            }
            words.Add(new Word(normal.ToString(), start, i));
        }
        return words;
    }

    // The normalised words joined by one space. Compares two strings as words.
    public static string Normalise(string text) => string.Join(' ', Split(text).Select(w => w.Text));

    // The normalised words run together, apostrophes dropped: how a choice is matched.
    public static string Squashed(string text) => string.Concat(Split(text).Select(w => w.Text.Replace("'", "")));

    // The scene, cut at each . ! or ?. A sentence with no words is dropped.
    public static IReadOnlyList<Sentence> Sentences(string text)
    {
        var sentences = new List<Sentence>();
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && !EndsASentenceAt(text, i))
                continue;

            var end = Math.Min(i + 1, text.Length);
            var words = Split(text[start..end]).Select(w => w with { Start = w.Start + start, End = w.End + start }).ToList();
            if (words.Count > 0)
                sentences.Add(new Sentence(text[start..end].Trim(), words));
            start = end;
        }
        return sentences;
    }

    // True for "He knows. He left": a sentence ends with words still to come. A full stop at the
    // very end of a quote is not a second sentence.
    public static bool EndsASentenceBeforeTheLastWord(string quote)
    {
        var words = Split(quote);
        return words.Count > 0 && Enumerable.Range(0, words[^1].Start).Any(i => EndsASentenceAt(quote, i));
    }

    // Where `part` starts as a run of whole words inside `whole`, or -1.
    public static int IndexOf(IReadOnlyList<Word> whole, IReadOnlyList<Word> part)
    {
        for (var at = 0; at + part.Count <= whole.Count; at++)
        {
            var matches = true;
            for (var k = 0; k < part.Count && matches; k++)
                matches = whole[at + k].Text == part[k].Text;
            if (matches)
                return at;
        }
        return -1;
    }

    private static bool IsApostrophe(char c) => c is '\'' or '’' or '‘';

    // A . ! or ? ends a sentence when nothing but closing marks follow it, or a space and then a
    // capital or an opening quote. So "owes 3.5 gold" and "He... knows a man" stay one sentence.
    // "Mr. Hobb" still splits: a missed quote is rolled, which is safe.
    private static bool EndsASentenceAt(string text, int i)
    {
        if (text[i] is not ('.' or '!' or '?'))
            return false;

        var next = i + 1;
        while (next < text.Length && text[next] is '.' or '!' or '?' or '"' or '\'' or '\u201D' or '\u2019' or ')')
            next++;
        if (next == text.Length)
            return true;
        if (!char.IsWhiteSpace(text[next]))
            return false;

        while (next < text.Length && char.IsWhiteSpace(text[next]))
            next++;
        return next == text.Length || char.IsUpper(text[next]) || text[next] is '"' or '\u201C' or '\'' or '\u2018';
    }
}
