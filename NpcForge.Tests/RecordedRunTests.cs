namespace NpcForge.Tests;

public class RecordedRunTests
{
    // The brief is last in the writer's message (ChatAgent.FirstMessage), so a brace in the scene or
    // in a name, both typed by people or models, must not be taken for the brief's start.
    [Fact]
    public void The_brief_is_found_from_the_end_whatever_braces_the_scene_and_the_name_hold()
    {
        const string brief = """{"setting":"a mill","tone":"Comic"}""";
        var name = new NamedCharacter(["Ke{", "sa"], "Va}le");

        var message = ChatAgent.FirstMessage(brief, "They see {a sign} on the door.", name);

        Assert.Equal(brief, RecordedRun.BriefIn(message));
    }
}
