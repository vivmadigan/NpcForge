using System.Xml.Linq;

namespace NpcForge.Tests;

// ADR-019, decision 1: NpcForge.Adapters holds the adapters two front ends share, so it references the core
// and the vendor packages and never a front end, which would put one front end inside the other. It reads
// the project files and not the compiled assembly, for the reason CoreReferenceTests gives: the compiler
// drops a reference no code uses. It names what is allowed, the core, and not the front ends, so a front
// end added later is refused with no change here. A reference back to a front end that uses the adapters
// already breaks the build, as a cycle or as a duplicate in the deps file, with an error that does not say
// why. This test holds the rule for a front end that does not use them, and says why when it fails.
public class AdaptersReferenceTests
{
    private static readonly string AdaptersDir = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Adapters");

    private static readonly string CoreProject = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Core", "NpcForge.Core.csproj");

    // The two ways a project file names another project's code. Packages are the vendors' and are not
    // limited here.
    private static readonly string[] ReferenceKinds = ["ProjectReference", "Reference"];

    [Fact]
    public void The_adapters_reference_no_project_but_the_core()
    {
        var references = BuildFiles()
            .SelectMany(file => XDocument.Load(file).Descendants()
                .Where(element => ReferenceKinds.Contains(element.Name.LocalName) && element.Attribute("Include") is not null)
                .Select(element => (File: file, Kind: element.Name.LocalName, Include: element.Attribute("Include")!.Value)))
            .ToList();
        bool IsTheCore((string File, string Kind, string Include) reference) =>
            reference.Kind == "ProjectReference"
            && string.Equals(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(reference.File)!, reference.Include)), Path.GetFullPath(CoreProject), StringComparison.OrdinalIgnoreCase);

        var others = references.Where(reference => !IsTheCore(reference))
            .Select(reference => $"{Path.GetRelativePath(ConsoleProcess.RepoRoot, reference.File)} has {reference.Kind} '{reference.Include}'")
            .ToList();
        Assert.True(others.Count == 0,
            $"{string.Join(", ", others)}. NpcForge.Adapters may reference the core and the vendor packages, and no other project. "
            + "The console and the website's API both build its adapters, so a front end there puts one front end inside the other (ADR-019, decision 1). "
            + "Remove the reference. A rule both front ends need goes in NpcForge.Core, and an adapter one front end uses stays in that front end.");

        // The adapters cannot build without the core, so finding no reference to it means this test read
        // the wrong files and would pass whatever they held.
        Assert.True(references.Any(IsTheCore),
            $"No ProjectReference to the core in [{string.Join(", ", BuildFiles().Select(file => Path.GetRelativePath(ConsoleProcess.RepoRoot, file)))}], "
            + "so the check above read nothing it could hold. Point AdaptersDir at the adapters' project.");
    }

    // The project and any props or targets inside its folder. A Directory.Build.props at the repo root
    // reaches the core as well, and CoreReferenceTests already refuses any reference there.
    private static IEnumerable<string> BuildFiles() =>
        Directory.EnumerateFiles(AdaptersDir, "*", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Where(file => Path.GetExtension(file) is ".csproj" or ".props" or ".targets");
}
