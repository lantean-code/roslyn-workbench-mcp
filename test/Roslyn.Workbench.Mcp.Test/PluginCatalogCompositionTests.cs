namespace Roslyn.Workbench.Mcp.Test;

public sealed class PluginCatalogCompositionTests
{
    [Fact]
    public void GIVEN_StartupComposition_WHEN_CreatingLoader_THEN_ShouldReturnConfiguredLoader()
    {
        var result = PluginCatalogComposition.CreateLoader();

        result.Should().NotBeNull();
    }
}
