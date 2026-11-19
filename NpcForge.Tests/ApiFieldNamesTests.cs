using System.Runtime.CompilerServices;          // CallerFilePath
using System.Text.RegularExpressions;

namespace NpcForge.Tests;

// ADR-019, decision 8: the page's list of the brief's field names is held to the roll's own. The pages
// are TypeScript and no .NET type reaches them, so this test is what stops a field added to the roll
// from being missing in the page, or two of them swapped, with every other test green. It reads
// NpcForge.Web/src/api.ts as text and parses nothing but that one list.
public class ApiFieldNamesTests
{
    // Found from this file's own path, which the compiler writes in, and not from AppContext.BaseDirectory:
    // a build in a long folder hands the app a \\?\ folder, and a walk up with ".." finds nothing there
    // (lesson 015).
    private static string ApiTs([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "NpcForge.Web", "src", "api.ts"));

    [Fact]
    public void The_pages_list_of_brief_fields_is_the_rolls_fields_in_the_rolls_order()
    {
        var source = File.ReadAllText(ApiTs());

        var list = Regex.Match(source, @"export const briefFields = \[(?<names>[^\]]*)\] as const;");
        Assert.True(list.Success, "src/api.ts has no `export const briefFields = [...] as const;`");
        var names = Regex.Matches(list.Groups["names"].Value, @"'(?<name>[^']+)'").Select(name => name.Groups["name"].Value);

        Assert.Equal(Dice.Fields.Select(field => field.Name), names);
    }
}
