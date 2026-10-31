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
        new("setting", "Where", null, WhenMissing.Ask),
        new("playersWant", "What the players want", null, WhenMissing.NotGiven),
        new("occupation", "Who", null, WhenMissing.Ask),
        new("difficulty", "How hard", ["Easy", "SomeWork", "Wall"], WhenMissing.Roll),
        new("characterWants", "What they want", null, WhenMissing.Roll),
        new("obstacle", "Why not", ["Wont", "Cant", "ForAPrice"], WhenMissing.Roll),
        new("attitude", "How they come across", null, WhenMissing.Roll),
        new("mannerism", "A habit", null, WhenMissing.Roll),
        new("weakness", "A weakness", null, WhenMissing.Roll),
        new("willNotDiscuss", "A subject", null, WhenMissing.Roll),
        new("wrongAbout", "A false belief", null, WhenMissing.Roll),
        new("tone", "How it is written", ["Serious", "Comic", "Heartfelt"], WhenMissing.Roll),
        new("race", "Their people", null, WhenMissing.Roll),
        new("nameLetter", "First name letter", Alphabet, WhenMissing.Roll),
        new("nameSyllables", "First name syllables", ["1", "2", "3"], WhenMissing.Roll),
        new("familyLetter", "Family name letter", Alphabet, WhenMissing.Roll),
    ];

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
        var line = Asked(draft).Lines().Single(l => l.Field.Name == "difficulty");
        Assert.Equal("Wall", line.Value);                // the server's spelling, not the model's
        Assert.Equal(Label.Inferred, line.Label);
        Assert.Equal(new Quoted("doesn't want to get involved", SecondSentence), line.Evidence);
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
        Assert.Equal(scene, ((Quoted)draft.Answers["wrongAbout"].Evidence).Sentence);
    }

    // ADR-009: tone and syllables are short choices, so they are inferred as ADR-007 allows.
    [Fact]
    public void A_tone_read_from_how_the_scene_is_told_is_kept_and_labelled_inferred()
    {
        var draft = Draft("The players meet a clown at a fair. Everything about him is a joke.");

        var result = draft.Record("tone", "comic", "Everything about him is a joke");

        Assert.Equal(Rejection.None, result.Reason);
        var line = Asked(draft).Lines().Single(l => l.Field.Name == "tone");
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

    // ADR-009, amended 2026-10-26: a list of 26 letters is never read from a scene, because "a" and
    // "I" are letters and words alike. Even a GM who says the letter outright gets it from the dice
    // or the flag, so no sentence can be made to look like it named one.
    [Theory]
    [InlineData("nameLetter", "K", "starts with K")]
    [InlineData("nameLetter", "A", "meet a tavern owner")]
    [InlineData("familyLetter", "I", "I think")]
    public void A_letter_is_never_recorded_from_a_scene_and_is_left_to_the_dice(string field, string value, string quote)
    {
        var draft = Draft("My players meet a tavern owner. I think her name starts with K.");

        var result = draft.Record(field, value, quote);

        Assert.Equal(Rejection.LongChoiceNotFromAScene, result.Reason);
        Assert.Empty(draft.Answers);
        Assert.Equal(Label.Rolled, Asked(draft).Lines().Single(l => l.Field.Name == field).Label);
    }

    // The rule reads the count from the field's own choices, so it holds for a field the console has
    // never heard of, and stops at six.
    [Theory]
    [InlineData(6, Rejection.None)]
    [InlineData(7, Rejection.LongChoiceNotFromAScene)]
    public void The_long_choice_rule_follows_the_fields_choice_count(int choices, Rejection expected)
    {
        var field = new RollField("mood", "A mood", [.. Enumerable.Range(1, choices).Select(n => $"mood{n}")], WhenMissing.Roll);
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
        Assert.Equal(Label.Rolled, Asked(draft).Lines().Single(l => l.Field.Name == "mannerism").Label);
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
        Assert.Equal(new Answer("Old Miller", Label.MyWords, new Quoted("meet the Old Miller", "The players meet the Old Miller at the Mill.")),
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

        Assert.Equal(Label.Rolled, Asked(draft).Lines().Single(l => l.Field.Name == "mannerism").Label);
        Assert.False(Asked(draft).RollArguments().ContainsKey("mannerism"));
    }

    // The Ask fields the scene never answered are what the interview must still ask (ADR-010, decision 1).
    [Fact]
    public void An_ask_field_the_scene_never_answered_is_still_to_ask_in_schema_order()
    {
        var draft = Draft();
        Assert.Equal(["setting", "occupation"], draft.StillToAsk.Select(f => f.Name));

        draft.Record("occupation", "tavern owner", "meet a tavern owner");

        Assert.Equal(["setting"], draft.StillToAsk.Select(f => f.Name));
    }

    // A hole cannot be confirmed or rolled: Lines throws and names the field.
    [Fact]
    public void An_unanswered_ask_field_cannot_be_confirmed_or_rolled()
    {
        var draft = Draft();

        var lines = Assert.Throws<InvalidOperationException>(() => draft.Lines());
        var roll = Assert.Throws<InvalidOperationException>(() => draft.RollArguments());

        Assert.Contains("'setting'", lines.Message);
        Assert.Contains("'setting'", roll.Message);
    }

    [Fact]
    public void A_not_given_field_with_no_answer_is_labelled_not_given_and_goes_to_the_roll_as_null()
    {
        var draft = Asked(Draft());

        var line = draft.Lines().Single(l => l.Field.Name == "playersWant");
        var arguments = draft.RollArguments();

        Assert.Equal(Label.NotGiven, line.Label);
        Assert.Null(line.Value);
        Assert.True(arguments.ContainsKey("playersWant"));
        Assert.Null(arguments["playersWant"]);
    }

    [Fact]
    public void The_roll_takes_every_recorded_answer_and_nothing_else()
    {
        var draft = Draft();
        draft.Record("setting", "his tavern", "at his tavern");
        draft.Record("occupation", "tavern owner", "meet a tavern owner");
        draft.Record("obstacle", "Cant", "knows nothing about the guild");
        draft.Record("attitude", "doesn't want to get involved", "doesn't want to get involved");
        draft.Record("playersWant", "ask about a thieves guild", "to ask about a thieves guild");

        // No difficulty: the scene never gave one, so the server's dice roll it (ADR-010).
        Assert.Equal(
            new Dictionary<string, object?>
            {
                ["setting"] = "his tavern",
                ["playersWant"] = "ask about a thieves guild",
                ["occupation"] = "tavern owner",
                ["obstacle"] = "Cant",
                ["attitude"] = "doesn't want to get involved",
            },
            draft.RollArguments());
    }

    // ADR-010, decision 4: what the GM typed when asked is theirs, labelled and evidenced as such. The
    // model's door still wants a quote the GM typed, so the two doors are not the same check.
    [Fact]
    public void RecordAsked_accepts_an_answer_the_model_door_would_refuse()
    {
        var draft = Draft();

        var refused = draft.Record("setting", "a harbour", "a harbour");
        var accepted = draft.RecordAsked("setting", "  a harbour  ");

        Assert.Equal(Rejection.QuoteNotTyped, refused.Reason);
        Assert.Equal(Rejection.None, accepted.Reason);
        Assert.Equal(new Answer("a harbour", Label.MyWords, new Asked()), draft.Answers["setting"]);
    }

    [Theory]
    [InlineData("mannerism", "taps the bar", Rejection.NotAnAskField)]      // Roll
    [InlineData("playersWant", "a name", Rejection.NotAnAskField)]          // NotGiven
    [InlineData("nope", "x", Rejection.UnknownField)]
    [InlineData("setting", "   ", Rejection.BlankAnswer)]
    public void RecordAsked_refuses_a_field_that_is_not_asked_for_and_a_blank_value(string field, string typed, Rejection reason)
    {
        var draft = Draft();

        var result = draft.RecordAsked(field, typed);

        Assert.Equal(reason, result.Reason);
        Assert.Empty(draft.Answers);
    }

    // Answers every Ask field still open, as the interview would, so a test can ask for Lines().
    private static SceneDraft Asked(SceneDraft draft)
    {
        foreach (var field in draft.StillToAsk)
            draft.RecordAsked(field.Name, "typed " + field.Name);
        return draft;
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
    public void The_one_normalising_function_treats_case_apostrophes_and_punctuation_alike()
    {
        Assert.Equal("doesn't want to get involved", Words.Normalise("  “Doesn’t want, to get INVOLVED!” "));
        Assert.Equal("dinner", Words.Normalise("dinner"));
    }
}
