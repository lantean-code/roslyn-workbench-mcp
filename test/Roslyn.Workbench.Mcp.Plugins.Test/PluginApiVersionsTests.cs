namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginApiVersionsTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_V1PluginApi_WHEN_ComparingCompatibilityContract_THEN_ShouldRetainExactValue()
    {
        PluginApiVersions.V1.Should().Be("1.0");
    }
}
