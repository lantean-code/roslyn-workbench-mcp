using System.Reflection;
using System.Xml.Linq;

namespace Roslyn.Workbench.Mcp.Test.Architecture;

public sealed class HostArchitectureTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_HostAssembly_WHEN_InspectingReleaseIdentity_THEN_ShouldKeepPublicVersionAndProvenanceSeparate()
    {
        var assembly = typeof(HostCommandLine).Assembly;
        var informationalVersion = assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .Single()
            .InformationalVersion;
        var fileVersion = assembly
            .GetCustomAttributes<AssemblyFileVersionAttribute>()
            .Single()
            .Version;
        var metadata = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(static attribute => attribute.Key, static attribute => attribute.Value, StringComparer.Ordinal);
        var coreVersion = informationalVersion.Split(['-', '+'], 2)[0];
        var expectedAssemblyVersion = $"{coreVersion}.0";

        assembly.GetName().Version.Should().Be(new Version(expectedAssemblyVersion));
        fileVersion.Should().Be(expectedAssemblyVersion);
        metadata["RoslynWorkbenchSourceTag"].Should().Be(informationalVersion);
        metadata["RoslynWorkbenchFullSemVer"].Should().NotBeNullOrWhiteSpace();
        metadata["RoslynWorkbenchCommitSha"].Should().NotBeNullOrWhiteSpace();
        int.TryParse(metadata["RoslynWorkbenchVersionSourceDistance"], NumberStyles.None, CultureInfo.InvariantCulture, out _).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_HostProject_WHEN_InspectingToolPackageMetadata_THEN_ShouldMatchApprovedIdentity()
    {
        var document = LoadProductionProject("Roslyn.Workbench.Mcp");
        var expectedProperties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PackAsTool"] = "true",
            ["ToolCommandName"] = "roslyn-workbench-mcp",
            ["PackageId"] = "Roslyn.Workbench.Mcp",
            ["AssemblyTitle"] = "Roslyn Workbench MCP",
            ["Product"] = "Roslyn Workbench MCP",
            ["Title"] = "Roslyn Workbench MCP",
            ["Description"] = "A local MCP server for Roslyn-powered C# code analysis and safe, transactional refactoring.",
            ["Authors"] = "Lantean Code",
            ["Company"] = "Lantean Code",
            ["Copyright"] = "Copyright © 2026 Lantean Code",
            ["PackageLicenseExpression"] = "MIT",
            ["PackageProjectUrl"] = "https://lantean-code.github.io/roslyn-workbench-mcp/",
            ["PackageIcon"] = "roslyn-workbench-mcp-128.png",
            ["PackageReadmeFile"] = "README.md",
            ["SymbolPackageFormat"] = "snupkg",
        };

        foreach (var expectedProperty in expectedProperties)
        {
            ReadProperty(document, expectedProperty.Key).Should().Be(expectedProperty.Value);
        }
    }

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

    private static string ReadProperty(XDocument document, string propertyName)
    {
        return document
            .Descendants(propertyName)
            .Single()
            .Value;
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
