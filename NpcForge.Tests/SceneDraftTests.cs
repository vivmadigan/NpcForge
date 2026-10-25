namespace NpcForge.Tests;

// ADR-007's checks and labels, free and pure: no model, no server, no process. Every refusal asserts
// its reason, not just that it refused (lesson 004).
public class SceneDraftTests
{
    // The epic's scene, word for word.
    public const string Tavern =
        "My players meet a tavern owner at his tavern to ask about a thieves guild. " +
        "He knows nothing about the guild, knows a few people who might, and doesn't want to get involved.";

    private const string SecondSentence =
        "He knows nothing about the guild, knows a few people who might, and doesn't want to get involved.";

    // roll_character's fields as the server lists them today. RollFieldTests holds this to the real
    // server; these tests stay pure.
    // Declared first: static fields start in the order they are written, and Fields reads this.
    private static readonly string[] Alphabet = [.. Enumerable.Range('A', 26).Select(c => ((char)c).ToString())];

    public static readonly IReadOnlyList<RollField> Fields =
    [
        new("setting", "Where", null, Required: true),
        new("playersWant", "What the players want", null, Required: true),
        new("difficulty", "How hard", ["Easy", "SomeWork", "Wall"], Required: true),
        new("occupation", "Who", null, Required: true),
        new("characterWants", "What they want", null, Required: false),
        new("obstacle", "Why not", ["Wont", "Cant", "ForAPrice"], Required: false),
        new("attitude", "How they come across", null, Required: false),
        new("mannerism", "A habit", null, Required: false),
        new("weakness", "A weakness", null, Required: false),
        new("willNotDiscuss", "A subject", null, Required: false),
        new("wrongAbout", "A false belief", null, Required: false),
        new("tone", "How it is written", ["Serious", "Comic", "Heartfelt"], Required: false),
        new("race", "Their people", null, Required: false),
        new("nameLetter", "First name letter", Alphabet, Required: false),
        new("nameSyllables", "First name syllables", ["1", "2", "3"], Required: false),
        new("familyLetter", "Family name letter", Alphabet, Required: false),
    ];

    public static readonly IReadOnlyDictionary<string, object?> Defaults = RunOptions.Parse([]).RollArguments();

    private static SceneDraft Draft(string scene = Tavern) => new(scene, Fields);

    // Done when: a quote I never typed is rejected.
    [Fact]
    public void A_quote_the_GM_never_typed_is_rejected()
    {
        var draft = Draft();

        var result = draft.Record("weakness", "drinks too much", "he drinks too much");

        Assert.Equal(Rejection.QuoteNotTyped, result.Reason);
        Assert.Empty(draft.Answers);
    }

    [Fact]
    public void A_quote_found_only_inside_a_longer_word_is_rejected()
    {
        var draft = Draft("The players meet him over dinner.");

        var result = draft.Record("setting", "inn", "inn");

        Assert.Equal(Rejection.QuoteNotTyped, result.Reason);
    }

    [Fact]
    public void The_whole_scene_as_a_quote_is_rejected()
    {
        var result = Draft().Record("setting", "his tavern", Tavern);

        Assert.Equal(Rejection.QuoteTooLong, result.Reason);
    }

    [Fact]
    public void A_quote_of_more_than_twelve_words_in_one_sentence_is_rejected()
    {
        // 13 words, all in the second sentence.
        var result = Draft().Record("attitude", "doesn't want to get involved",
            "knows nothing about the guild, knows a few people who might, and doesn't want");

        Assert.Equal(Rejection.QuoteTooLong, result.Reason);
    }

    [Fact]
    public void A_quote_that_runs_across_two_sentences_is_rejected()
    {
        // Every word is in the scene, in this order, but the full stop between them was dropped.
        var result = Draft().Record("occupation", "tavern owner", "a thieves guild He knows nothing");

        Assert.Equal(Rejection.QuoteTooLong, result.Reason);
    }

    [Fact]
    public void A_quote_holding_two_sentences_is_rejected()
    {
        var result = Draft("He is tall. He is kind.").Record("attitude", "kind", "He is tall. He is kind");

        Assert.Equal(Rejection.QuoteTooLong, result.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("\"\"")]
    public void An_empty_quote_is_rejected(string quote)
    {
        var result = Draft().Record("difficulty", "Wall", quote);

        Assert.Equal(Rejection.EmptyQuote, result.Reason);
    }

    // The honest variants: case, a full stop, a curly apostrophe, extra spaces and quote marks are
    // not words, so the quote is still the GM's.
    [Theory]
    [InlineData("doesn't want to get involved")]
    [InlineData("Doesn't want to get involved.")]
    [InlineData("doesn’t want to get involved")]
    [InlineData("doesn't  want   to get  involved")]
    [InlineData("'doesn't want to get involved'")]
    [InlineData("“Doesn’t want to get INVOLVED.”")]
    public void An_honest_quote_that_differs_only_in_case_punctuation_and_spaces_is_accepted(string quote)
    {
        var draft = Draft();

        var result = draft.Record("attitude", "doesn't want to get involved", quote);

        Assert.Equal(Rejection.None, result.Reason);
        Assert.Equal("doesn't want to get involved", draft.Answers["attitude"].Value);
    }

    // Done when: an inferred choice is kept and labelled.
    [Fact]
    public void A_difficulty_inferred_from_the_GMs_words_is_kept_and_labelled_inferred()
    {
        var draft = Draft();

        var result = draft.Record("difficulty", "wall", "doesn't want to get involved");

        Assert.Equal(Rejection.None, result.Reason);
        var line = draft.Lines(Defaults).Single(l => l.Field.Name == "difficulty");
        Assert.Equal("Wall", line.Value);                // the server's spelling, not the model's
        Assert.Equal(Label.Inferred, line.Label);
        Assert.Equal("doesn't want to get involved", line.Quote);
        Assert.Equal(SecondSentence, line.Sentence);
    }

    // A real model may spell a choice as the description reads: "Can't", "Some Work".
    [Theory]
    [InlineData("obstacle", "Can't", "Cant")]
    [InlineData("obstacle", "won’t", "Wont")]
    [InlineData("obstacle", "for a price", "ForAPrice")]
    [InlineData("difficulty", "Some Work", "SomeWork")]
    public void A_choice_spelled_with_spaces_or_an_apostrophe_is_stored_as_the_server_spells_it(string field, string value, string stored)
    {
        var draft = Draft();

        var result = draft.Record(field, value, "doesn't want to get involved");

        Assert.Equal(Rejection.None, result.Reason);
        Assert.Equal(stored, draft.Answers[field].Value);
    }

    // A full stop that does not end a sentence: a number, an ellipsis.
    [Theory]
    [InlineData("He owes 3.5 gold to the guild.", "owes 3.5 gold", "3.5 gold")]
    [InlineData("He... knows a man who might.", "He... knows a man", "knows a man")]
    public void A_full_stop_inside_a_sentence_does_not_split_it(string scene, string quote, string value)
    {
        var draft = Draft(scene);

        var result = draft.Record("wrongAbout", value, quote);

        Assert.Equal(Rejection.None, result.Reason);
        Assert.Equal(scene, draft.Answers["wrongAbout"].Sentence);
    }

    // ADR-009: tone and syllables are short choices, so they are inferred as ADR-007 allows.
    [Fact]
    public void A_tone_read_from_how_the_scene_is_told_is_kept_and_labelled_inferred()
    {
        var draft = Draft("The players meet a clown at a fair. Everything about him is a joke.");

        var result = draft.Record("tone", "comic", "Everything about him is a joke");

        Assert.Equal(Rejection.None, result.Reason);
        var line = draft.Lines(Defaults).Single(l => l.Field.Name == "tone");
        Assert.Equal("Comic", line.Value);
        Assert.Equal(Label.Inferred, line.Label);
    }

    [Fact]
    public void A_syllable_count_is_inferred_without_its_digit_in_the_quote()
    {
        var draft = Draft("The players meet a smith. She has a short name.");

        var result = draft.Record("nameSyllables", "1", "She has a short name");

        Assert.Equal(Rejection.None, result.Reason);
        Assert.Equal("1", draft.Answers["nameSyllables"].Value);
    }

    // ADR-009: a list of 26 letters is not one the GM can judge at a glance, so the letter has to be
    // in the GM's words. Done when: K on a quote with no K is rejected, and kept on "starts with K".
    [Theory]
    [InlineData("nameLetter")]
    [InlineData("familyLetter")]
    public void A_letter_the_quote_never_says_is_rejected_and_left_to_the_dice(string field)
    {
        var draft = Draft();

        var result = draft.Record(field, "K", "meet a tavern owner at his tavern");

        Assert.Equal(Rejection.ChoiceNotInQuote, result.Reason);
        Assert.Empty(draft.Answers);
        Assert.Equal(Label.Rolled, draft.Lines(Defaults).Single(l => l.Field.Name == field).Label);
    }

    [Theory]
    [InlineData("nameLetter", "K", "starts with K")]
    [InlineData("nameLetter", "k", "starts with K")]
    [InlineData("familyLetter", "K", "family name starts with K")]
    public void A_letter_the_GM_says_as_a_word_is_kept_and_labelled_inferred(string field, string value, string quote)
    {
        var draft = Draft("Her name starts with K. Her family name starts with K.");

        var result = draft.Record(field, value, quote);

        Assert.Equal(Rejection.None, result.Reason);
        var line = draft.Lines(Defaults).Single(l => l.Field.Name == field);
        Assert.Equal("K", line.Value);
        Assert.Equal(Label.Inferred, line.Label);
    }

    // "Kessa" holds a K but is not the word K: the letter has to be said, not found inside a name.
    [Fact]
    public void A_letter_inside_a_longer_word_does_not_count()
    {
        var draft = Draft("Her name is Kessa.");

        var result = draft.Record("nameLetter", "K", "Her name is Kessa");

        Assert.Equal(Rejection.ChoiceNotInQuote, result.Reason);
    }

    // The rule reads the count from the field's own choices, so it holds for a field the console has
    // never heard of, and stops at six.
    [Theory]
    [InlineData(6, Rejection.None)]
    [InlineData(7, Rejection.ChoiceNotInQuote)]
    public void The_long_choice_rule_follows_the_fields_choice_count(int choices, Rejection expected)
    {
        var field = new RollField("mood", "A mood", [.. Enumerable.Range(1, choices).Select(n => $"mood{n}")], Required: false);
        var draft = new SceneDraft(Tavern, [field]);

        var result = draft.Record("mood", "mood1", "doesn't want to get involved");

        Assert.Equal(expected, result.Reason);
    }

    [Fact]
    public void A_race_is_free_text_and_must_be_the_GMs_words()
    {
        var draft = Draft("The players meet a dwarf smith at her forge.");

        Assert.Equal(Rejection.None, draft.Record("race", "dwarf", "meet a dwarf smith").Reason);
        Assert.Equal(Label.MyWords, draft.Answers["race"].Label);
        Assert.Equal(Rejection.ValueNotInQuote, draft.Record("race", "gnome", "meet a dwarf smith").Reason);
    }

    [Fact]
    public void A_choice_not_in_the_list_is_rejected()
    {
        var result = Draft().Record("obstacle", "Scared", "doesn't want to get involved");

        Assert.Equal(Rejection.ValueNotInList, result.Reason);
    }

    // Done when: a free-text value that is not a span of my words is rejected. The reviewers' case.
    [Fact]
    public void An_invented_mannerism_with_a_real_quote_attached_is_rejected()
    {
        var draft = Draft();

        var result = draft.Record("mannerism", "taps the bar nervously", "doesn't want to get involved");

        Assert.Equal(Rejection.ValueNotInQuote, result.Reason);
        Assert.Equal(Label.Rolled, draft.Lines(Defaults).Single(l => l.Field.Name == "mannerism").Label);
    }

    [Fact]
    public void A_free_text_value_must_be_whole_words_of_its_quote_in_order()
    {
        // Every word is in the quote, but not as one run.
        var result = Draft().Record("attitude", "want involved", "doesn't want to get involved");

        Assert.Equal(Rejection.ValueNotInQuote, result.Reason);
    }

    [Fact]
    public void A_free_text_value_is_stored_as_the_GM_typed_it_and_labelled_my_words()
    {
        var draft = Draft("The players meet the Old Miller at the Mill.");

        var result = draft.Record("occupation", "old miller", "meet the old miller");

        Assert.Equal(Rejection.None, result.Reason);
        Assert.Equal(new Answer("Old Miller", "meet the Old Miller", "The players meet the Old Miller at the Mill.", Label.MyWords),
            draft.Answers["occupation"]);
    }

    [Fact]
    public void An_unknown_field_is_rejected()
    {
        var result = Draft().Record("hairColour", "red", "his tavern");

        Assert.Equal(Rejection.UnknownField, result.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" , ")]
    public void An_empty_value_is_rejected(string value)
    {
        var result = Draft().Record("setting", value, "at his tavern");

        Assert.Equal(Rejection.EmptyValue, result.Reason);
    }

    // Done when: a field I said nothing about is rolled.
    [Fact]
    public void A_field_the_GM_said_nothing_about_is_rolled_and_left_out_of_the_roll()
    {
        var draft = Draft();
        draft.Record("obstacle", "Cant", "knows nothing about the guild");

        Assert.Equal(Label.Rolled, draft.Lines(Defaults).Single(l => l.Field.Name == "mannerism").Label);
        Assert.False(draft.RollArguments(Defaults).ContainsKey("mannerism"));
    }

    [Fact]
    public void A_required_field_the_GM_said_nothing_about_takes_its_flag_default()
    {
        var draft = Draft();

        var line = draft.Lines(Defaults).Single(l => l.Field.Name == "occupation");

        Assert.Equal(Label.Default, line.Label);
        Assert.Equal("innkeeper", line.Value);
        Assert.Equal("innkeeper", draft.RollArguments(Defaults)["occupation"]);
    }

    [Fact]
    public void The_roll_takes_every_recorded_answer_and_the_defaults_and_nothing_else()
    {
        var draft = Draft();
        draft.Record("setting", "his tavern", "at his tavern");
        draft.Record("occupation", "tavern owner", "meet a tavern owner");
        draft.Record("obstacle", "Cant", "knows nothing about the guild");
        draft.Record("attitude", "doesn't want to get involved", "doesn't want to get involved");

        Assert.Equal(
            new Dictionary<string, object?>
            {
                ["setting"] = "his tavern",
                ["playersWant"] = "the name of a fence",
                ["difficulty"] = "Wall",
                ["occupation"] = "tavern owner",
                ["obstacle"] = "Cant",
                ["attitude"] = "doesn't want to get involved",
            },
            draft.RollArguments(Defaults));
    }

    [Fact]
    public void A_second_answer_replaces_the_first_and_a_rejection_changes_nothing()
    {
        var draft = Draft();
        draft.Record("setting", "tavern", "at his tavern");
        draft.Record("setting", "his tavern", "at his tavern");
        draft.Record("setting", "a castle", "a castle");

        Assert.Equal("his tavern", draft.Answers["setting"].Value);
    }

    [Fact]
    public void A_required_field_with_no_default_stops_the_roll()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Draft().RollArguments(new Dictionary<string, object?>()));

        Assert.Contains("requires 'setting'", error.Message);
    }

    [Fact]
    public void The_one_normalising_function_treats_case_apostrophes_and_punctuation_alike()
    {
        Assert.Equal("doesn't want to get involved", Words.Normalise("  “Doesn’t want, to get INVOLVED!” "));
        Assert.Equal("dinner", Words.Normalise("dinner"));
    }
}
