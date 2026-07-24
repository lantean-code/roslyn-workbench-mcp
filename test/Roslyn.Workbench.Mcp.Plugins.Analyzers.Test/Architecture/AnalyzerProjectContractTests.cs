using System.Reflection;
using System.Runtime.Versioning;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers.Test.Architecture;

public sealed class AnalyzerProjectContractTests
{
    private static readonly string[] _forbiddenAssemblyPrefixes =
    [
        "Microsoft.CodeAnalysis.Workspaces",
        "Roslyn.Workbench.Mcp.Plugins",
        "Roslyn.Workbench.Mcp.Workspace",
        "System.Composition",
    ];

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_AnalyzerAssembly_WHEN_InspectingTargetFramework_THEN_ShouldTargetNetStandard20()
    {
        var analyzerAssembly = LoadAnalyzerAssembly();
        var targetFramework = analyzerAssembly.GetCustomAttribute<TargetFrameworkAttribute>();

        Assert.NotNull(targetFramework);
        var frameworkName = targetFramework.FrameworkName;

        frameworkName.Should().Be(".NETStandard,Version=v2.0");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_AnalyzerAssembly_WHEN_InspectingDependencies_THEN_ShouldNotReferenceRuntimeOrWorkspaceAssemblies()
    {
        var analyzerAssembly = LoadAnalyzerAssembly();
        var referencedAssemblies = analyzerAssembly.GetReferencedAssemblies();
        foreach (var reference in referencedAssemblies)
        {
            if (reference.Name is not { } referenceName)
            {
                continue;
            }

            foreach (var forbiddenPrefix in _forbiddenAssemblyPrefixes)
            {
                var hasForbiddenPrefix = referenceName.StartsWith(
                    forbiddenPrefix,
                    StringComparison.Ordinal);

                hasForbiddenPrefix.Should().BeFalse();
            }
        }
    }

    private static Assembly LoadAnalyzerAssembly()
    {
        var assembly = Assembly.Load("Roslyn.Workbench.Mcp.Plugins.Analyzers");
        return assembly;
    }
}
