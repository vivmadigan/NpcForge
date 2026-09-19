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
        public required string Occupation { get; init; }      // who they are; not in BUILD.md, see PLAN.md decisions

        // rolled on the server, inside what Difficulty allows
        public required string CharacterWants { get; init; }
        public required ObstacleKind Obstacle { get; init; }
        public required string Attitude { get; init; }
        public required string Mannerism { get; init; }
        public required string Weakness { get; init; }
        public required string WillNotDiscuss { get; init; }
        public required string WrongAbout { get; init; }
    }

    // The MCP SDK already sends "Wall" on the wire. Plain JsonSerializer sends 2, and step 8's
    // file uses plain JsonSerializer. On the type, every serializer writes the name.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Difficulty { Easy, SomeWork, Wall }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ObstacleKind { Wont, Cant, ForAPrice }

}
