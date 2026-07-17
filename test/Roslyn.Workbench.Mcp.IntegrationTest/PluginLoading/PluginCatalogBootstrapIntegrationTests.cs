namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCatalogBootstrapIntegrationTests
{
    [Fact]
    public void GIVEN_BundledPluginAssembly_WHEN_LoadingCatalogue_THEN_ShouldReturnConfiguredSnapshot()
    {
        var target = new PluginCatalogBootstrap();

        var result = target.Load(new StartupOptions(), [typeof(BundledCorePlugin).Assembly]);

        result.Tools.Should().HaveCount(41);
        result.Plugins.Should().ContainSingle(plugin => plugin.PluginId == "roslyn.workbench.core" && plugin.Enabled);
    }
}
