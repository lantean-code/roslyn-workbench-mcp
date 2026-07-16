using System.Reflection;
using System.Xml.Linq;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Architecture;

public sealed class CodeActionsArchitectureTests
{
    [Fact]
    public void GIVEN_ProductionProjects_WHEN_InspectingProjectReferences_THEN_ShouldMatchApprovedDependencyGraph()
    {
        var expectedReferences = new Dictionary<string, string[]>
        {
            ["Roslyn.Workbench.Mcp.Workspace"] = [],
            ["Roslyn.Workbench.Mcp.CodeActions"] = ["Roslyn.Workbench.Mcp.Workspace"],
            ["Roslyn.Workbench.Mcp.Plugins"] = ["Roslyn.Workbench.Mcp.Workspace"],
            ["Roslyn.Workbench.Mcp.Plugins.Core"] =
            [
                "Roslyn.Workbench.Mcp.Plugins",
                "Roslyn.Workbench.Mcp.Workspace",
            ],
            ["Roslyn.Workbench.Mcp"] =
            [
                "Roslyn.Workbench.Mcp.CodeActions",
                "Roslyn.Workbench.Mcp.Plugins",
                "Roslyn.Workbench.Mcp.Plugins.Core",
                "Roslyn.Workbench.Mcp.Workspace",
            ],
        };

        foreach (var project in expectedReferences)
        {
            var document = LoadProductionProject(project.Key);
            var actualReferences = ReadProjectNames(document, "ProjectReference");

            actualReferences.Should().BeEquivalentTo(project.Value);
        }
    }

    [Fact]
    public void GIVEN_CodeActionsProject_WHEN_InspectingReferences_THEN_ShouldContainNoPluginHostOrMcpDependencies()
    {
        var document = LoadProductionProject("Roslyn.Workbench.Mcp.CodeActions");

        var projectReferences = ReadProjectNames(document, "ProjectReference");
        var packageReferences = ReadItemIncludes(document, "PackageReference");

        projectReferences.Should().Equal("Roslyn.Workbench.Mcp.Workspace");
        packageReferences.Should().NotContain("ModelContextProtocol");
    }

    [Fact]
    public void GIVEN_CodeActionsProject_WHEN_InspectingFriendAssemblies_THEN_ShouldContainOnlyDirectConsumers()
    {
        var document = LoadProductionProject("Roslyn.Workbench.Mcp.CodeActions");
        var friendAssemblies = document
            .Descendants("AssemblyAttribute")
            .Where(static element => string.Equals(
                element.Attribute("Include")?.Value,
                "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
                StringComparison.Ordinal))
            .Elements("_Parameter1")
            .Select(static element => element.Value)
            .ToArray();

        friendAssemblies.Should().BeEquivalentTo(
        [
            "Roslyn.Workbench.Mcp",
            "Roslyn.Workbench.Mcp.IntegrationTestSupport",
            "Roslyn.Workbench.Mcp.CodeActions.Test",
            "Roslyn.Workbench.Mcp.CodeActions.IntegrationTest",
            "Roslyn.Workbench.Mcp.CodeActions.AuditTest",
            "Roslyn.Workbench.Mcp.Test",
            "Roslyn.Workbench.Mcp.IntegrationTest",
            "DynamicProxyGenAssembly2",
        ]);
    }

    private static XDocument LoadProductionProject(string projectName)
    {
        var projectPath = Path.Combine(GetRepositoryRoot(), "src", projectName, $"{projectName}.csproj");
        return XDocument.Load(projectPath);
    }

    private static IReadOnlyList<string> ReadProjectNames(XDocument document, string itemName)
    {
        return ReadItemIncludes(document, itemName)
            .Select(static include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .ToArray();
    }

    private static IReadOnlyList<string> ReadItemIncludes(XDocument document, string itemName)
    {
        return document
            .Descendants(itemName)
            .Select(static element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();
    }

    private static string GetRepositoryRoot()
    {
        var repositoryRoot = typeof(CodeActionsArchitectureTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == "RepositoryRoot")
            .Value;
        return repositoryRoot ?? throw new InvalidOperationException("RepositoryRoot assembly metadata was not configured.");
    }
}
