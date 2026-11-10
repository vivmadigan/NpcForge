using System.Text.Json;

namespace NpcForge.Tests;

// Dice.Roll(arguments), in process: what it binds. The flags, the interview, the lab and a saved brief
// each hand the roll a dictionary, and each must keep binding the way it did before the roll learned
// to refuse by name (ADR-016). These pins were run on the binder before that change, and a probe of
// the same inputs gave the same briefs on Microsoft.Extensions.AI.Abstractions 10.8.3, 10.10.0 and
// 10.11.0. Each pin rolls the same seed twice, once with the value as sent and once as the brief
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
}
