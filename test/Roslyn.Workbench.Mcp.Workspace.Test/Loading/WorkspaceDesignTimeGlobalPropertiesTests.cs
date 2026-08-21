using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceDesignTimeGlobalPropertiesTests
{
    [Fact]
    public void GIVEN_NoRequestedProperties_WHEN_Creating_THEN_ShouldReturnRoslynDesignTimeDefaults()
    {
        var result = WorkspaceDesignTimeGlobalProperties.Create(globalProperties: null);

        result.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["DesignTimeBuild"] = bool.TrueString,
            ["NonExistentFile"] = "__NonExistentSubDir__\\__NonExistentFile__",
            ["BuildProjectReferences"] = bool.FalseString,
            ["BuildingProject"] = bool.FalseString,
            ["ProvideCommandLineArgs"] = bool.TrueString,
            ["SkipCompilerExecution"] = bool.TrueString,
            ["ContinueOnError"] = "ErrorAndContinue",
            ["ShouldUnsetParentConfigurationAndPlatform"] = bool.FalseString,
        });
    }

    [Fact]
    public void GIVEN_RequestedProperties_WHEN_Creating_THEN_ShouldRetainThemAndOverrideDefaults()
    {
        var globalProperties = new Dictionary<string, string>
        {
            ["Configuration"] = "Release",
            ["DesignTimeBuild"] = bool.FalseString,
        };

        var result = WorkspaceDesignTimeGlobalProperties.Create(globalProperties);

        result["Configuration"].Should().Be("Release");
        result["DesignTimeBuild"].Should().Be(bool.FalseString);
    }
}
