using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace NpcForge.Server
{
    public sealed record CharacterBrief
    {
        // you supply these four
        public required string Setting { get; init; }
        public required string PlayersWant { get; init; }
        public required Difficulty Difficulty { get; init; }
        public required string Occupation { get; init; }      // who they are; supplied, not rolled (ADR-002)

        // rolled on the server, inside what Difficulty allows
        public required string CharacterWants { get; init; }
        public required ObstacleKind Obstacle { get; init; }
        public required string Attitude { get; init; }
        public required string Mannerism { get; init; }
        public required string Weakness { get; init; }
        public required string WillNotDiscuss { get; init; }
        public required string WrongAbout { get; init; }

        // ADR-009. Tone changes only the writing. Race and the three name dice are inputs to the
        // namer. Each is the GM's, inferred where ADR-007 allows, or rolled; the model never picks.
        public required Tone Tone { get; init; }
        public required string Race { get; init; }
        public required NameLetter NameLetter { get; init; }
        public required Syllables NameSyllables { get; init; }
        public required NameLetter FamilyLetter { get; init; }
    }

    // The MCP SDK already sends "Wall" on the wire. Plain JsonSerializer sends 2, and step 8's
    // file uses plain JsonSerializer. On the type, every serializer writes the name.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Difficulty { Easy, SomeWork, Wall }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ObstacleKind { Wont, Cant, ForAPrice }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Tone { Serious, Comic, Heartfelt }

    // A letter of the alphabet, written as itself. Both the first name and the family name use it.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NameLetter { A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z }

    // A number cannot be an enum member name, so each member is written as its digit on the wire and
    // in the schema. The console reads the count with int.Parse and keeps no copy of this enum.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Syllables
    {
        [JsonStringEnumMemberName("1")] One = 1,
        [JsonStringEnumMemberName("2")] Two = 2,
        [JsonStringEnumMemberName("3")] Three = 3,
    }

}
