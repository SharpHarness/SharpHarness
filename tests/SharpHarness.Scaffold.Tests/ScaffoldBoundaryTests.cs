using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharpHarness.Scaffold.Tests;

public sealed class ScaffoldBoundaryTests
{
    private static readonly string[] SourceProjects =
    [
        "src/SharpHarness.Abstractions/SharpHarness.Abstractions.csproj",
        "src/SharpHarness.Core/SharpHarness.Core.csproj",
        "src/SharpHarness.Models/SharpHarness.Models.csproj",
        "src/SharpHarness.Tools/SharpHarness.Tools.csproj",
        "src/SharpHarness.Infrastructure/SharpHarness.Infrastructure.csproj",
        "src/SharpHarness.Coding/SharpHarness.Coding.csproj",
        "src/SharpHarness.Cli/SharpHarness.Cli.csproj",
    ];

    private const string TestProject =
        "tests/SharpHarness.Scaffold.Tests/SharpHarness.Scaffold.Tests.csproj";

    private static readonly string[] TestPackages =
    [
        "Microsoft.NET.Test.Sdk",
        "xunit",
        "xunit.runner.visualstudio",
    ];

    private static readonly Dictionary<string, string[]> AllowedReferences =
        new(StringComparer.Ordinal)
        {
            ["SharpHarness.Abstractions"] = [],
            ["SharpHarness.Core"] = ["SharpHarness.Abstractions"],
            ["SharpHarness.Models"] = ["SharpHarness.Abstractions"],
            ["SharpHarness.Tools"] = ["SharpHarness.Abstractions"],
            ["SharpHarness.Infrastructure"] = ["SharpHarness.Abstractions"],
            ["SharpHarness.Coding"] =
            [
                "SharpHarness.Abstractions",
                "SharpHarness.Core",
                "SharpHarness.Tools",
            ],
            ["SharpHarness.Cli"] =
            [
                "SharpHarness.Abstractions",
                "SharpHarness.Core",
                "SharpHarness.Models",
                "SharpHarness.Tools",
                "SharpHarness.Infrastructure",
                "SharpHarness.Coding",
            ],
            ["SharpHarness.Scaffold.Tests"] =
            [
                "SharpHarness.Abstractions",
                "SharpHarness.Core",
                "SharpHarness.Models",
                "SharpHarness.Tools",
                "SharpHarness.Infrastructure",
                "SharpHarness.Coding",
                "SharpHarness.Cli",
            ],
        };

    [Fact]
    public void Solution_lists_the_seven_scaffold_projects_and_the_test_project()
    {
        var root = RepositoryLayout.Root();
        var solutionPath = Path.Combine(root, "SharpHarness.slnx");
        var solution = XDocument.Load(solutionPath);
        var listed = solution
            .Descendants("Project")
            .Select(element => Normalize(element.Attribute("Path")?.Value))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var expected = SourceProjects
            .Append(TestProject)
            .Select(Normalize)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, listed);
        foreach (var relative in expected)
        {
            var absolute = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(absolute), $"Solution entry is missing on disk: {relative}");
        }
    }

    [Fact]
    public void Projects_are_net10_sdk_class_libraries()
    {
        var root = RepositoryLayout.Root();
        var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        Assert.Equal(["net10.0"], PropertyValues(props, "TargetFramework"));
        Assert.Equal(["enable"], PropertyValues(props, "Nullable"));
        Assert.Equal(["true"], PropertyValues(props, "TreatWarningsAsErrors"));
        Assert.Equal(["false"], PropertyValues(props, "IsPackable"));

        foreach (var relative in SourceProjects.Append(TestProject))
        {
            var project = XDocument.Load(ProjectPath(root, relative));
            var sdk = project.Root?.Attribute("Sdk")?.Value;
            Assert.Equal("Microsoft.NET.Sdk", sdk);
            Assert.Empty(PropertyValues(project, "TargetFramework"));
            Assert.DoesNotContain(
                PropertyValues(project, "OutputType"),
                value => value.Equals("Exe", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                PropertyValues(project, "TargetFrameworks"),
                value => value.Contains("windows", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Project_references_stay_inside_the_allowed_direction_and_are_not_speculative()
    {
        var root = RepositoryLayout.Root();
        var graph = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var relative in SourceProjects.Append(TestProject))
        {
            var project = XDocument.Load(ProjectPath(root, relative));
            var name = Path.GetFileNameWithoutExtension(relative);
            var references = project
                .Descendants("ProjectReference")
                .Select(element => Normalize(element.Attribute("Include")?.Value))
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .OrderBy(reference => reference, StringComparer.Ordinal)
                .ToArray();

            Assert.True(AllowedReferences.ContainsKey(name), $"No dependency rule for {name}.");
            Assert.All(references, reference => Assert.Contains(reference, AllowedReferences[name]));
            graph.Add(name, references);
        }

        AssertNoCycles(graph);

        foreach (var references in graph.Values)
            Assert.Empty(references);
    }

    [Fact]
    public void Source_projects_have_no_packages_or_runtime_implementation_files()
    {
        var root = RepositoryLayout.Root();
        var srcRoot = Path.Combine(root, "src");
        var sourceFiles = RepositoryLayout
            .EnumerateFiles(srcRoot)
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            SourceProjects.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            sourceFiles);

        foreach (var relative in SourceProjects)
        {
            var project = XDocument.Load(ProjectPath(root, relative));
            Assert.Empty(PackageNames(project));
            Assert.DoesNotContain(
                "Microsoft.Data.Sqlite",
                File.ReadAllText(ProjectPath(root, relative)),
                StringComparison.OrdinalIgnoreCase);
        }

        var testProject = XDocument.Load(ProjectPath(root, TestProject));
        Assert.Equal(TestPackages, PackageNames(testProject).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Empty(
            testProject
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value));
    }

    [Fact]
    public void Global_json_pins_the_net10_feature_band()
    {
        var root = RepositoryLayout.Root();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        var sdk = document.RootElement.GetProperty("sdk");

        Assert.Equal("10.0.402", sdk.GetProperty("version").GetString());
        Assert.Equal("latestPatch", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [Fact]
    public void Ci_workflow_runs_restore_build_and_test_on_three_operating_systems()
    {
        var root = RepositoryLayout.Root();
        var workflowPath = Path.Combine(root, ".github", "workflows", "ci.yml");
        var workflows = RepositoryLayout
            .EnumerateFiles(Path.Combine(root, ".github"))
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([".github/workflows/ci.yml"], workflows);

        var yaml = File.ReadAllText(workflowPath);
        Assert.Contains("ubuntu-latest", yaml, StringComparison.Ordinal);
        Assert.Contains("windows-latest", yaml, StringComparison.Ordinal);
        Assert.Contains("macos-latest", yaml, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(yaml, "feat/t001-dotnet10-bootstrap"));
        Assert.Equal(2, CountOccurrences(yaml, "- main"));
        Assert.Contains("dotnet restore SharpHarness.slnx", yaml, StringComparison.Ordinal);
        Assert.Contains("dotnet build SharpHarness.slnx", yaml, StringComparison.Ordinal);
        Assert.Contains("dotnet test SharpHarness.slnx", yaml, StringComparison.Ordinal);
        Assert.Contains("global-json-file: global.json", yaml, StringComparison.Ordinal);
        Assert.Contains("contents: read", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", yaml, StringComparison.Ordinal);

        var pins = Regex.Matches(yaml, @"uses:\s+\S+@([0-9a-fA-F]+)");
        Assert.Equal(2, pins.Count);
        foreach (Match pin in pins)
            Assert.Matches("^[0-9a-f]{40}$", pin.Groups[1].Value);
    }

    private static void AssertNoCycles(IReadOnlyDictionary<string, string[]> graph)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in graph.Keys.OrderBy(key => key, StringComparer.Ordinal))
            Visit(name);

        void Visit(string name)
        {
            if (!visiting.Add(name))
                Assert.Fail($"Project reference cycle includes {name}.");

            if (!visited.Add(name))
            {
                visiting.Remove(name);
                return;
            }

            if (graph.TryGetValue(name, out var references))
            {
                foreach (var reference in references)
                    Visit(reference);
            }

            visiting.Remove(name);
        }
    }

    private static string ProjectPath(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string[] PropertyValues(XDocument document, string name) =>
        document.Descendants(name).Select(element => element.Value.Trim()).ToArray();

    private static string[] PackageNames(XDocument document) =>
        document
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();

    private static string Normalize(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim();

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

internal static class RepositoryLayout
{
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharpHarness.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"SharpHarness.slnx was not found above {AppContext.BaseDirectory}.");
    }

    public static IEnumerable<string> EnumerateFiles(string directory)
    {
        if (!Directory.Exists(directory))
            yield break;

        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relative = path.Replace('\\', '/');
            if (relative.Contains("/bin/", StringComparison.Ordinal)
                || relative.Contains("/obj/", StringComparison.Ordinal)
                || relative.EndsWith("/.DS_Store", StringComparison.Ordinal)
                || relative.EndsWith("/Thumbs.db", StringComparison.Ordinal))
            {
                continue;
            }

            yield return path;
        }
    }
}
