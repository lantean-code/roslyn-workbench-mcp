using Roslyn.Workbench.Mcp.Test.PluginLoading;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class PluginPackageDiscoveryIntegrationTests
{
    [Fact]
    public void GIVEN_PluginDirectoryAssemblies_WHEN_LoadingCatalog_THEN_ShouldKeepEnabledToolsAndDisabledDiagnostics()
    {
        var startupOptions = CreateStartupOptions(GetPluginDirectory("Valid"));
        using var snapshot = PluginCatalogLoaderTestFactory.Load(startupOptions, []);

        var tools = snapshot.Tools;
        var plugins = snapshot.Plugins;

        tools.Select(static tool => tool.Tool.Metadata.Name).Should().BeEquivalentTo([
            "host-query-cache-calibration",
            "host-valid-mutation",
            "host-valid-query",
        ]);

        plugins.Should().ContainSingle(static status => status.PluginId == "host.valid.query" && status.Enabled);
        plugins.Should().ContainSingle(static status => status.PluginId == "host.valid.mutation" && status.Enabled);
        plugins.Should().ContainSingle(static status => !status.Enabled && status.Diagnostics.Count > 0);
    }

    [Fact]
    public void GIVEN_PluginToolCollidesWithReservedCodeActionName_WHEN_LoadingCatalog_THEN_ShouldDisablePluginWithDiagnostic()
    {
        using var snapshot = PluginCatalogLoaderTestFactory.Load(
            CreateStartupOptions(GetPluginDirectory("ReservedCollision")),
            [],
            ["host-valid-query"]);

        snapshot.Tools.Should().BeEmpty();
        snapshot.Plugins.Should().ContainSingle(status =>
            status.PluginId == "host.valid.query"
            && !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginCollision"
                && diagnostic.Message.Contains("collide", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GIVEN_MultiplePackagesWithSamePluginId_WHEN_LoadingCatalog_THEN_ShouldDisableEveryPackageDeterministically()
    {
        using var snapshot = PluginCatalogLoaderTestFactory.Load(CreateStartupOptions(GetPluginDirectory("Duplicate")), []);

        snapshot.Tools.Should().BeEmpty();
        snapshot.Plugins.Should().HaveCount(2);
        snapshot.Plugins.Should().OnlyContain(status =>
            status.PluginId == "host.valid.query"
            && !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginCollision"
                && diagnostic.Message.Contains("same plugin ID", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GIVEN_NoExternalPluginDirectory_WHEN_LoadingBundledCore_THEN_ShouldComposeBundledCatalogueInDefaultContext()
    {
        using var snapshot = PluginCatalogLoaderTestFactory.Load(
            new StartupOptions(),
            [typeof(BundledCorePlugin).Assembly]);

        snapshot.Tools.Should().HaveCount(PluginCatalogLoaderTestFactory.BundledCoreToolCount);
        snapshot.Plugins.Should().ContainSingle(status => status.PluginId == "roslyn.workbench.core" && status.Enabled);
        snapshot.LoadContexts.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_SingleExportPluginConfigureThrows_WHEN_LoadingCatalog_THEN_ShouldDisablePluginWithoutPublishingExceptionDetails()
    {
        using var snapshot = PluginCatalogLoaderTestFactory.Load(CreateStartupOptions(GetPluginDirectory("Throwing")), []);

        snapshot.Tools.Should().BeEmpty();
        snapshot.Plugins.Should().ContainSingle(status =>
            status.PluginId == "test.throwing.configuration"
            && !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginLoad"
                && diagnostic.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
                && !diagnostic.Message.Contains("Configuration failed", StringComparison.Ordinal)));
    }

    private static string GetPluginDirectory(string scenario)
    {
        return Path.Combine(AppContext.BaseDirectory, "PluginFixtureAssets", "Catalog", scenario);
    }

    private static StartupOptions CreateStartupOptions(string pluginDirectory)
    {
        return new StartupOptions
        {
            PluginDirectories = [pluginDirectory],
            DefaultMaxResults = 100,
        };
    }
}
