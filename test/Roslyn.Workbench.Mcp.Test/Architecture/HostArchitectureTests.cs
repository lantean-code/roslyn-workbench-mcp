using System.Reflection;
using System.Xml.Linq;

namespace Roslyn.Workbench.Mcp.Test.Architecture;

public sealed class HostArchitectureTests
{
    [Fact]
    public void GIVEN_ProductionProjects_WHEN_InspectingMcpPackageOwnership_THEN_ShouldRestrictSdkToHost()
    {
        var projectNames = new[]
        {
            "Roslyn.Workbench.Mcp.Workspace",
            "Roslyn.Workbench.Mcp.CodeActions",
            "Roslyn.Workbench.Mcp.Plugins",
            "Roslyn.Workbench.Mcp.Plugins.Core",
            "Roslyn.Workbench.Mcp",
        };

        var owners = projectNames
            .Where(projectName => ReadItemIncludes(LoadProductionProject(projectName), "PackageReference")
                .Contains("ModelContextProtocol", StringComparer.Ordinal))
            .ToArray();

        owners.Should().Equal("Roslyn.Workbench.Mcp");
    }

    [Fact]
    public void GIVEN_HostProject_WHEN_InspectingFriendAssemblies_THEN_ShouldContainOnlyDirectConsumers()
    {
        var document = LoadProductionProject("Roslyn.Workbench.Mcp");

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
            "Roslyn.Workbench.Mcp.Test",
            "Roslyn.Workbench.Mcp.IntegrationTest",
            "Roslyn.Workbench.Mcp.IntegrationTestSupport",
            "DynamicProxyGenAssembly2",
        ]);
    }

    private static XDocument LoadProductionProject(string projectName)
    {
        var projectPath = Path.Combine(GetRepositoryRoot(), "src", projectName, $"{projectName}.csproj");
        return XDocument.Load(projectPath);
    }

    private static string[] ReadItemIncludes(XDocument document, string itemName)
    {
        return document
            .Descendants(itemName)
            .Select(static element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToArray();
    }

    private static string GetRepositoryRoot()
    {
        var repositoryRoot = typeof(HostArchitectureTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == "RepositoryRoot")
            .Value;

        return repositoryRoot ?? throw new InvalidOperationException("RepositoryRoot assembly metadata was not configured.");
    }
}
