using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NpcForge.Tests;

// ADR-014, decision 7: NpcForge.Core is the code the console and a website share, so it must not reach a
// front end, a model vendor, MCP, the web stack or a file. The compiler holds the file rule through
// BannedSymbols.txt and RS0030. These tests hold what the compiler cannot: that nothing new is referenced,
// and that nobody switches the rule off. They read the project file, not the compiled assembly, because the
// compiler drops a reference the code never uses, so a reference added to show the test failing would
// pass for the wrong reason. They read every MSBuild and editor-config file that applies to the core, the
// project's own and each one in a folder above it up to the repo root, because a Directory.Build.props
// reaches the core without a line in its project file. No test here starts a process or runs msbuild.
public class CoreReferenceTests
{
    private static readonly string CoreDir = Path.Combine(ConsoleProcess.RepoRoot, "NpcForge.Core");

    private static readonly string CoreProject = Path.Combine(CoreDir, "NpcForge.Core.csproj");

    private static readonly string BannedSymbols = Path.Combine(CoreDir, "BannedSymbols.txt");

    private static readonly string[] Allowed = ["Microsoft.Extensions.AI.Abstractions", "Microsoft.CodeAnalysis.BannedApiAnalyzers"];

    private static readonly string[] ReferenceKinds = ["PackageReference", "ProjectReference", "Reference", "FrameworkReference"];

    // The properties that switch the analyzers off, and with them the banned-API rule.
    private static readonly string[] AnalyzerSwitches = ["RunAnalyzers", "RunAnalyzersDuringBuild"];

    // The one file that may suppress the rule: SkillFiles reads the bundled skills, and it is the core's
    // one door to the disk (ADR-014, decision 6).
    private const string SuppressionDoor = "SkillFiles.cs";

    [Fact]
    public void The_core_is_a_plain_class_library_with_no_other_sdk()
    {
        var project = XDocument.Load(CoreProject).Root!;
        var others = project.Descendants()
            .Where(element => element.Name.LocalName == "Sdk" || element.Attribute("Sdk") is not null)
            .Select(element => element.Name.LocalName == "Sdk" ? element.Attribute("Name")?.Value : element.Attribute("Sdk")!.Value)
            .Where(sdk => sdk != "Microsoft.NET.Sdk")
            .ToList();
        var own = project.Attribute("Sdk")?.Value;

        Assert.True(own == "Microsoft.NET.Sdk" && others.Count == 0,
            $"NpcForge.Core.csproj has <Project Sdk=\"{own}\"> and other SDKs [{string.Join(", ", others)}]. The core's SDK is exactly Microsoft.NET.Sdk. "
            + "A web, worker or other SDK brings its own framework references and a host, so the core could reach what ADR-014, decision 7, keeps out, "
            + "whatever its ProjectReference and PackageReference lines say. Change it back, or write an ADR.");
    }

    [Fact]
    public void The_core_references_only_what_the_allow_list_names()
    {
        var offenders = BuildFiles()
            .SelectMany(file => XDocument.Load(file).Descendants()
                .Where(element => ReferenceKinds.Contains(element.Name.LocalName))
                .Select(element => (File: Relative(file), Kind: element.Name.LocalName, Name: element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? element.Attribute("Remove")?.Value ?? "(no Include)")))
            .Where(reference => !Allowed.Contains(reference.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(offenders.Count == 0,
            $"{string.Join(", ", offenders.Select(o => $"{o.File} has {o.Kind} '{o.Name}'"))}, which is not on the allow-list ({string.Join(", ", Allowed)}). "
            + "The core is what a console and a website both call, so a front end, a model vendor, MCP or a web reference there lets it reach what ADR-014 keeps out, "
            + "used or not, and a reference in a Directory.Build.props above the project reaches it as surely as one in the project file. Remove the reference. Widening the list needs an ADR.");
    }

    [Fact]
    public void The_banned_api_rule_stays_an_error_in_the_core()
    {
        var errors = XDocument.Load(CoreProject).Descendants("WarningsAsErrors").Select(element => element.Value);

        Assert.True(errors.Any(value => value.Contains("RS0030")),
            "NpcForge.Core.csproj no longer lists RS0030 in WarningsAsErrors. Without it a banned call is a warning the build prints and passes, "
            + "so BannedSymbols.txt stops holding the core's boundary (ADR-014, decision 7).");
    }

    [Fact]
    public void The_banned_symbols_file_is_an_analyzer_input_of_the_core()
    {
        var items = BuildFiles().SelectMany(file => XDocument.Load(file).Descendants()
            .Where(element => element.Name.LocalName == "AdditionalFiles")
            .Select(element => (File: file, Element: element))).ToList();
        bool IsBannedSymbols(string? path) =>
            path is not null && string.Equals(Path.GetFullPath(Path.Combine(CoreDir, path)), Path.GetFullPath(BannedSymbols), StringComparison.OrdinalIgnoreCase);

        var included = items.Any(item => IsBannedSymbols(item.Element.Attribute("Include")?.Value));
        var removed = items.Where(item => IsBannedSymbols(item.Element.Attribute("Remove")?.Value)).Select(item => Relative(item.File)).ToList();

        Assert.True(File.Exists(BannedSymbols) && included && removed.Count == 0,
            $"BannedSymbols.txt is not an AdditionalFiles item of the core (file exists: {File.Exists(BannedSymbols)}, an <AdditionalFiles Include> for it: {included}, removed in: [{string.Join(", ", removed)}]). "
            + "The banned-API analyzer reads its list only from an AdditionalFiles item, so without one RS0030 has nothing to ban and the core may call the file system, the network and the terminal "
            + "while the build stays green (ADR-014, decision 7). Put <AdditionalFiles Include=\"BannedSymbols.txt\" /> back in NpcForge.Core.csproj.");
    }

    [Fact]
    public void The_analyzers_are_never_switched_off_for_the_core()
    {
        var found = BuildFiles().SelectMany(file => XDocument.Load(file).Descendants()
                .Where(element => AnalyzerSwitches.Contains(element.Name.LocalName) && !string.Equals(element.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                .Select(element => $"{Relative(file)} sets <{element.Name.LocalName}> to '{element.Value.Trim()}'"))
            .ToList();

        Assert.True(found.Count == 0,
            "The analyzers are switched off for the core:\n  " + string.Join("\n  ", found)
            + "\nRS0030 is an analyzer rule, so with RunAnalyzers or RunAnalyzersDuringBuild false the build never checks BannedSymbols.txt and stays green "
            + "(ADR-014, decision 7). Remove the property, or write an ADR.");
    }

    [Fact]
    public void The_banned_api_rule_is_suppressed_nowhere_in_the_core_but_the_skill_door()
    {
        var found = new List<string>();

        foreach (var file in Directory.EnumerateFiles(CoreDir, "*.cs", SearchOption.AllDirectories).Where(InSource))
        {
            if (Path.GetFileName(file) == SuppressionDoor)
                continue;

            var text = File.ReadAllText(file);
            if (Regex.IsMatch(text, @"#pragma\s+warning\s+disable[^\r\n]*RS0030"))
                found.Add($"{Relative(file)} has `#pragma warning disable` for RS0030");
            if (Regex.IsMatch(text, @"#pragma\s+warning\s+disable\s*(//[^\r\n]*)?(\r|\n|$)"))
                found.Add($"{Relative(file)} has a bare `#pragma warning disable`, which switches off every warning, RS0030 included");
            if (Regex.IsMatch(text, @"SuppressMessage\s*\([^)]*RS0030"))
                found.Add($"{Relative(file)} has a SuppressMessage for RS0030");
        }

        // A NoWarn or WarningsNotAsErrors in the core's project files, or in a Directory.Build.props,
        // Directory.Build.targets or Directory.Packages.props on the way up to the repo root.
        foreach (var file in BuildFiles())
            foreach (var element in XDocument.Load(file).Descendants())
            {
                var name = element.Name.LocalName;
                if (name is "NoWarn" or "WarningsNotAsErrors" && element.Value.Contains("RS0030"))
                    found.Add($"{Relative(file)} lists RS0030 in <{name}>");
            }

        // An .editorconfig or .globalconfig severity, in the core's folder or any folder above it.
        foreach (var file in ConfigFiles())
            foreach (var line in File.ReadLines(file).Select(l => l.Trim()).Where(l => !l.StartsWith('#') && !l.StartsWith(';')))
                if (line.Contains("RS0030"))
                    found.Add($"{Relative(file)} sets a severity for RS0030: {line}");

        Assert.True(found.Count == 0,
            "The banned-API rule (RS0030) is suppressed in the core:\n  " + string.Join("\n  ", found)
            + $"\nBannedSymbols.txt is how the compiler keeps files, the network, the environment and the terminal out of NpcForge.Core, so the rule has one door: {SuppressionDoor}, "
            + "which reads the bundled skills (ADR-014, decisions 6 and 7). Call the right door instead of suppressing the rule, or write an ADR.");
    }

    private static bool InSource(string path) =>
        !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj");

    private static string Relative(string path) => Path.GetRelativePath(ConsoleProcess.RepoRoot, path);

    // Folders from the core's own up to the repo root: what MSBuild and the editor settings read for it.
    private static IEnumerable<string> FoldersUpToRepoRoot()
    {
        for (var folder = new DirectoryInfo(CoreDir); folder is not null; folder = folder.Parent)
        {
            yield return folder.FullName;
            if (string.Equals(Path.GetFullPath(folder.FullName).TrimEnd('\\', '/'), Path.GetFullPath(ConsoleProcess.RepoRoot).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                yield break;
        }
    }

    // Every MSBuild file that applies to the core: the project and any props or targets inside its folder,
    // and the three files MSBuild picks up by name from each folder above it.
    private static IEnumerable<string> BuildFiles() =>
        Directory.EnumerateFiles(CoreDir, "*", SearchOption.AllDirectories)
            .Where(InSource)
            .Where(file => Path.GetExtension(file) is ".csproj" or ".props" or ".targets")
            .Concat(FoldersUpToRepoRoot()
                .SelectMany(folder => new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" }.Select(name => Path.Combine(folder, name)))
                .Where(File.Exists))
            .Distinct();

    private static IEnumerable<string> ConfigFiles() =>
        FoldersUpToRepoRoot().SelectMany(folder => new[] { ".editorconfig", ".globalconfig" }.Select(name => Path.Combine(folder, name))).Where(File.Exists)
            .Concat(Directory.EnumerateFiles(CoreDir, "*", SearchOption.AllDirectories).Where(InSource).Where(file => Path.GetFileName(file) is ".editorconfig" or ".globalconfig"))
            .Distinct();
}
