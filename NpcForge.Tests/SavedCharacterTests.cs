namespace NpcForge.Tests;

// A new save keeps its brief as the JSON a typed brief writes: camelCase names, the enums by name, and a
// null want written as null (ADR-009, ADR-010). StorageTests holds the bytes of the file this ends up in.
public class SavedCharacterTests
{
    [Fact]
    public void A_typed_brief_is_kept_with_camelCase_names_enum_names_and_a_null_want()
    {
        var brief = new CharacterBrief
        {
            Setting = "a harbour town",
            PlayersWant = null,
            Difficulty = Difficulty.SomeWork,
            Occupation = "ferryman",
            CharacterWants = "to be left alone",
            Obstacle = ObstacleKind.ForAPrice,
            Attitude = "tired",
            Mannerism = "hums",
            Weakness = "gambling",
            WillNotDiscuss = "his brother",
            WrongAbout = "the tides",
            Tone = Tone.Heartfelt,
            Race = "human",
            NameLetter = NameLetter.T,
            NameSyllables = Syllables.Two,
            FamilyLetter = NameLetter.B,
            Gender = Gender.Female,
            Age = Age.MiddleAged,
            Look = "a scar",
        };

        var saved = SavedCharacter.Of(brief, new CharacterName(["Tam", "sin"], "Brook"), "Text.");

        Assert.Equal("a harbour town", saved.Brief.GetProperty("setting").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, saved.Brief.GetProperty("playersWant").ValueKind);
        Assert.Equal("SomeWork", saved.Brief.GetProperty("difficulty").GetString());
        Assert.Equal("ForAPrice", saved.Brief.GetProperty("obstacle").GetString());
        Assert.Equal("Heartfelt", saved.Brief.GetProperty("tone").GetString());
        Assert.Equal("2", saved.Brief.GetProperty("nameSyllables").GetString());
        Assert.Equal("MiddleAged", saved.Brief.GetProperty("age").GetString());
        // Every public property of the brief is kept, and nothing else. The rule is the set, not how many
        // fields there are today, so a new brief field does not break it.
        Assert.Equal(
            typeof(CharacterBrief).GetProperties().Select(property => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(property.Name)).Order(StringComparer.Ordinal),
            saved.Brief.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Brook", saved.Name!.Family);
        Assert.Equal("Text.", saved.Text);
    }
}
