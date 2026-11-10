using System.Text.Json;

namespace NpcForge.Tests;

// Dice.Roll(arguments), in process: what it binds and what it refuses. The flags, the interview, the lab
// and a saved brief each hand the roll a dictionary, and each must keep binding the way it did before the
// roll learned to refuse by name (ADR-016). The pins were run on the binder before that change, and a
// probe of the same inputs gave the same briefs on Microsoft.Extensions.AI.Abstractions 10.8.3, 10.10.0
// and 10.11.0. Each pin rolls the same seed twice, once with the value as sent and once as the brief
// writes it, so a value that bound to anything else would roll a different brief.
public class RollArgumentsTests
{
    // The four answers every roll here starts from: the flags' defaults.
    internal static Dictionary<string, object?> RollArgs() => new()
    {
        ["setting"] = "an inn in a major city",
        ["playersWant"] = "the name of a fence",
        ["difficulty"] = "Wall",
        ["occupation"] = "innkeeper",
    };

    internal static Dictionary<string, object?> RollArgs(string handedIn, object? value)
    {
        var args = RollArgs();
        args[handedIn] = value;
        return args;
    }

    private static string Roll(Dictionary<string, object?> arguments) => BriefJson.Of(new Dice(1).Roll(arguments));

    // The brief text is what the writer is sent, so its escaping is part of what the model reads: an
    // apostrophe, a letter beyond ASCII, markup, a quote and an emoji each go as a backslash-u escape. The
    // expected text is what the MCP server's roll_character returned for these arguments under seed 1,
    // captured at a7dba4a before the server retired. The fixtures' briefs are plain ASCII and the goldens
    // compare values, not bytes, so nothing else holds this (ADR-016).
    [Fact]
    public void The_brief_text_escapes_what_the_server_escaped()
    {
        var brief = Roll(RollArgs("setting", "Gull's Rest at Ærø <stall> & \"q\" \U0001F642"));

        Assert.Equal(
            """{"setting":"Gull\u0027s Rest at \u00C6r\u00F8 \u003Cstall\u003E \u0026 \u0022q\u0022 \uD83D\uDE42","playersWant":"the name of a fence","difficulty":"Wall","occupation":"innkeeper","characterWants":"to keep a secret buried","obstacle":"Wont","attitude":"world-weary","mannerism":"tilts their head when listening","weakness":"cannot bear being left out","willNotDiscuss":"how they lost a tooth","wrongAbout":"thinks all adventurers know one another","tone":"Heartfelt","race":"human","nameLetter":"N","nameSyllables":"1","familyLetter":"F","gender":"Female","age":"MiddleAged","look":"unusual hair colour: two colours, half light and half dark"}""",
            brief);
    }

    private static JsonElement Field(string briefJson, string field) =>
        JsonDocument.Parse(briefJson).RootElement.GetProperty(field).Clone();

    // A flag goes to the roll as typed (RunOptions), so a GM's "wall" or " Wall " must still be Wall.
    [Theory]
    [InlineData("difficulty", "wall", "Wall")]
    [InlineData("difficulty", "WALL", "Wall")]
    [InlineData("difficulty", " Wall ", "Wall")]
    [InlineData("difficulty", "someWork", "SomeWork")]
    [InlineData("obstacle", "forAprice", "ForAPrice")]
    [InlineData("tone", "comic", "Comic")]
    [InlineData("nameLetter", "q", "Q")]
    [InlineData("familyLetter", "z", "Z")]
    [InlineData("gender", "female", "Female")]
    [InlineData("age", "middleaged", "MiddleAged")]
    public void An_enum_name_in_any_case_binds_as_the_name(string field, string sent, string written)
    {
        var brief = Roll(RollArgs(field, sent));

        Assert.Equal(written, Field(brief, field).GetString());
        Assert.Equal(Roll(RollArgs(field, written)), brief);
    }

    // The binder reads a number as the enum value it stands for, as text or as a number. --syllables
    // sends its count as text.
    [Theory]
    [InlineData("difficulty", "2", "Wall")]
    [InlineData("difficulty", 2, "Wall")]
    [InlineData("tone", "1", "Comic")]
    [InlineData("tone", 1, "Comic")]
    [InlineData("nameSyllables", "2", "2")]
    [InlineData("nameSyllables", 2, "2")]
    public void A_number_that_is_one_of_the_values_binds_as_that_value(string field, object sent, string written)
    {
        var brief = Roll(RollArgs(field, sent));

        Assert.Equal(written, Field(brief, field).GetString());
        Assert.Equal(Roll(RollArgs(field, written)), brief);
    }

    // A lab's tone variant hands every field of a brief back as a JsonElement (BriefJson.WithField), the
    // want's null among them. Every die is replaced, so the brief comes back whole with only the tone changed.
    [Fact]
    public void A_brief_handed_back_as_json_elements_rolls_itself_with_the_one_field_changed()
    {
        var notGiven = Roll(RollArgs("playersWant", null));

        var variant = Roll(BriefJson.WithField(notGiven, "tone", "Heartfelt"));

        var expected = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(notGiven)!;
        var actual = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(variant)!;
        Assert.Equal("Heartfelt", actual["tone"].GetString());
        Assert.Equal(JsonValueKind.Null, actual["playersWant"].ValueKind);
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var (name, was) in expected.Where(entry => entry.Key != "tone"))
            Assert.True(was.ToString() == actual[name].ToString(), $"{name} changed when the brief was handed back");
    }

    [Theory]
    [InlineData("difficulty", "\"wall\"", "Wall")]
    [InlineData("difficulty", "2", "Wall")]
    [InlineData("nameSyllables", "\"2\"", "2")]
    [InlineData("setting", "\"a ferry\"", "a ferry")]
    public void A_json_element_binds_as_the_value_it_holds(string field, string json, string written)
    {
        var sent = JsonDocument.Parse(json).RootElement.Clone();

        var brief = Roll(RollArgs(field, sent));

        Assert.Equal(written, Field(brief, field).GetString());
        Assert.Equal(Roll(RollArgs(field, written)), brief);
    }

    // ADR-010: the want is required and nullable. A null, plain or as a JsonElement, is "not given".
    [Fact]
    public void A_null_want_binds_as_not_given_however_it_is_sent()
    {
        var plain = Roll(RollArgs("playersWant", null));
        var element = Roll(RollArgs("playersWant", JsonDocument.Parse("null").RootElement.Clone()));

        Assert.Equal(JsonValueKind.Null, Field(plain, "playersWant").ValueKind);
        Assert.Equal(plain, element);
    }

    // The binder ignores a key the roll has no field for, the same name in another case included.
    [Fact]
    public void A_key_the_roll_has_no_field_for_is_ignored()
    {
        var args = RollArgs();
        args["pet"] = "a cat";
        args["Setting"] = "somewhere else";

        Assert.Equal(Roll(RollArgs()), Roll(args));
    }

    // ADR-016: the roll refuses a required field left out by name, before any die is cast. Before, the
    // binder threw an ArgumentException that named the parameter in a .NET sentence. The control is
    // RollArgs() itself, which every pin above rolls.
    [Theory]
    [InlineData("setting", "setting is missing, and the roll needs it: text.")]
    [InlineData("occupation", "occupation is missing, and the roll needs it: text.")]
    [InlineData("playersWant", "playersWant is missing, and the roll needs it: text, or null when it is not given.")]
    public void A_required_field_left_out_is_refused_by_name(string field, string reason)
    {
        var args = RollArgs();
        Assert.True(args.Remove(field));

        var refused = Assert.Throws<RollRefusedException>(() => Roll(args));

        Assert.Equal(reason, refused.Reason);
    }

    // ADR-016: a choice in words the field does not have is refused with the field's name and its values,
    // as the schema writes them. Before, "grumpy" reached the binder and came back as "Object of type
    // 'System.String' cannot be converted to type 'System.Nullable`1[NpcForge.Tone]'". "Two" is a member
    // name of the syllables enum, but the field's values are 1, 2 and 3.
    [Theory]
    [InlineData("tone", "grumpy", "Serious, Comic, Heartfelt")]
    [InlineData("difficulty", "Nonsense", "Easy, SomeWork, Wall")]
    [InlineData("difficulty", "Some Work", "Easy, SomeWork, Wall")]
    [InlineData("nameSyllables", "Two", "1, 2, 3")]
    [InlineData("age", "Middle Aged", "Teenager, Adult, MiddleAged, Old")]
    [InlineData("gender", "", "Male, Female")]
    public void A_choice_the_field_does_not_have_is_refused_with_its_values(string field, string sent, string values)
    {
        var refused = Assert.Throws<RollRefusedException>(() => Roll(RollArgs(field, sent)));

        Assert.Equal($"{field} {sent} is not one of its values: {values}.", refused.Reason);
    }

    // The same refusal for a choice sent as a JsonElement, the way a lab or a saved brief sends one.
    [Fact]
    public void A_choice_the_field_does_not_have_is_refused_when_it_comes_as_a_json_element()
    {
        var sent = JsonDocument.Parse("\"grumpy\"").RootElement.Clone();

        var refused = Assert.Throws<RollRefusedException>(() => Roll(RollArgs("tone", sent)));

        Assert.Equal("tone grumpy is not one of its values: Serious, Comic, Heartfelt.", refused.Reason);
    }

    // An enum number that is not one of the values is refused in the same words, with the values.
    [Theory]
    [InlineData("tone", "7", "tone 7 is not one of its values: Serious, Comic, Heartfelt.")]
    [InlineData("nameSyllables", 4, "nameSyllables 4 is not one of its values: 1, 2, 3.")]
    public void An_enum_number_that_is_not_a_value_is_refused_with_its_values(string field, object sent, string reason)
    {
        var refused = Assert.Throws<RollRefusedException>(() => Roll(RollArgs(field, sent)));

        Assert.Equal(reason, refused.Reason);
    }

    // Only those refusals are relabelled. A value of the wrong type is a caller's bug, so the binder's own
    // ArgumentException reaches the caller as it is, and the console does not show it as the GM's slip.
    [Fact]
    public void A_value_of_the_wrong_type_is_the_binders_own_error_and_not_a_refusal()
    {
        Assert.Throws<ArgumentException>(() => Roll(RollArgs("setting", 5)));
    }

    // The three below held the same rules against the server process in McpToolSourceTests until the
    // console called the core in-process (ADR-014). Their assertions are kept.

    // Done when: --tone 7 is refused by name. System.Text.Json's string-enum converter takes a number
    // too, so without the check it would bind and be written back as a number. Every enum parameter,
    // difficulty and obstacle included, and the number as text as well as a number (the console sends text).
    [Theory]
    [InlineData("tone", 7)]
    [InlineData("tone", "7")]
    [InlineData("difficulty", 9)]
    [InlineData("difficulty", "9")]
    [InlineData("obstacle", 9)]
    [InlineData("obstacle", "9")]
    [InlineData("nameLetter", 99)]
    [InlineData("nameLetter", "99")]
    [InlineData("nameSyllables", 4)]
    [InlineData("nameSyllables", "0")]
    [InlineData("familyLetter", 99)]
    [InlineData("familyLetter", "99")]
    [InlineData("gender", 9)]
    [InlineData("gender", "9")]
    [InlineData("age", 9)]
    [InlineData("age", "9")]
    public void An_enum_value_that_is_not_defined_is_refused_by_the_name_of_its_field(string parameter, object value)
    {
        var args = RollArgs();
        args[parameter] = value;

        var error = Assert.Throws<RollRefusedException>(() => Roll(args));

        // The reason, not just a failure (lesson 004): the roll's own words, naming the field.
        Assert.Contains($"{parameter} {value} is not one of its values", error.Message);
    }

    // ADR-011: a blank look would reach the writer as a look of nothing, so it is refused by name, as a
    // blank want is. The control is the same call with the look left out, which rolls one.
    [Fact]
    public void A_blank_look_is_refused_by_name_and_a_left_out_look_rolls()
    {
        foreach (var blank in new[] { "", "   " })
        {
            var error = Assert.Throws<RollRefusedException>(() => Roll(RollArgs("look", blank)));
            Assert.Contains("look is blank", error.Message);
        }
        var rolled = Roll(RollArgs());
        Assert.False(string.IsNullOrWhiteSpace(JsonDocument.Parse(rolled).RootElement.GetProperty("look").GetString()));
    }

    // "Not given" has one spelling. A blank want is refused in the roll's own words, by name.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_want_is_refused_by_name(string blank)
    {
        var args = RollArgs();
        args["playersWant"] = blank;

        var error = Assert.Throws<RollRefusedException>(() => Roll(args));

        Assert.Contains("playersWant is blank", error.Message);
    }
}
