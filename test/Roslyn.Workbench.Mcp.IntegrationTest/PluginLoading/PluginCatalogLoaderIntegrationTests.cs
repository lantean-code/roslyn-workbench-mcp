namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCatalogLoaderIntegrationTests
{
    [Fact]
    public void GIVEN_BundledPluginAssembly_WHEN_LoadingCatalogue_THEN_ShouldReturnConfiguredSnapshot()
    {
        var result = PluginCatalogLoaderTestFactory.Load(
            new StartupOptions(),
            [typeof(BundledCorePlugin).Assembly]);

        result.Tools.Should().HaveCount(PluginCatalogLoaderTestFactory.BundledCoreToolCount);
        result.Plugins.Should().ContainSingle(plugin => plugin.PluginId == "roslyn.workbench.core" && plugin.Enabled);
    }
}
