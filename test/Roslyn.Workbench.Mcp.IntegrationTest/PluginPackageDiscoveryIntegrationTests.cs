using System.Reflection;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class PluginPackageDiscoveryIntegrationTests
{
    [Fact]
    public void GIVEN_PluginDirectoryAssemblies_WHEN_LoadingCatalog_THEN_ShouldKeepEnabledToolsAndDisabledDiagnostics()
    {
        using var pluginDirectory = CreatePluginDirectory(
            typeof(HostValidQueryPlugin).Assembly,
            typeof(HostValidMutationPlugin).Assembly,
            typeof(ValidQueryTestPlugin).Assembly);

        var startupOptions = CreateStartupOptions(pluginDirectory.DirectoryPath);
        var bootstrap = new PluginCatalogBootstrap();
        var snapshot = bootstrap.Load(startupOptions, []);

        var tools = snapshot.Tools;
        var plugins = snapshot.Plugins;

        tools.Should().HaveCount(2);
        tools.Select(static tool => tool.Tool.Metadata.Name).Should().Contain(["host-valid-query", "host-valid-mutation"]);

        plugins.Should().ContainSingle(static status => status.PluginId == "host.valid.query" && status.Enabled);
        plugins.Should().ContainSingle(static status => status.PluginId == "host.valid.mutation" && status.Enabled);
        plugins.Should().ContainSingle(static status => !status.Enabled && status.Diagnostics.Count > 0);
    }

    [Fact]
    public void GIVEN_PluginToolCollidesWithReservedCodeActionName_WHEN_LoadingCatalog_THEN_ShouldDisablePluginWithDiagnostic()
    {
        using var pluginDirectory = CreatePluginDirectory(typeof(HostValidQueryPlugin).Assembly);
        var bootstrap = new PluginCatalogBootstrap();

        var snapshot = bootstrap.Load(
            CreateStartupOptions(pluginDirectory.DirectoryPath),
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
        using var pluginDirectory = CreatePluginDirectory(
            typeof(HostValidQueryPlugin).Assembly,
            typeof(HostValidQueryPlugin).Assembly);
        var bootstrap = new PluginCatalogBootstrap();

        var snapshot = bootstrap.Load(CreateStartupOptions(pluginDirectory.DirectoryPath), []);

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
        var bootstrap = new PluginCatalogBootstrap();

        var snapshot = bootstrap.Load(new StartupOptions(), [typeof(BundledCorePlugin).Assembly]);

        snapshot.Tools.Should().HaveCount(41);
        snapshot.Plugins.Should().ContainSingle(status => status.PluginId == "roslyn.workbench.core" && status.Enabled);
        snapshot.LoadContexts.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_SingleExportPluginConfigureThrows_WHEN_LoadingCatalog_THEN_ShouldDisablePluginWithoutPublishingExceptionDetails()
    {
        using var pluginDirectory = CreatePluginDirectory(typeof(ThrowingConfigurationTestPlugin).Assembly);
        var bootstrap = new PluginCatalogBootstrap();

        var snapshot = bootstrap.Load(CreateStartupOptions(pluginDirectory.DirectoryPath), []);

        snapshot.Tools.Should().BeEmpty();
        snapshot.Plugins.Should().ContainSingle(status =>
            status.PluginId == "test.throwing.configuration"
            && !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginLoad"
                && diagnostic.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
                && !diagnostic.Message.Contains("Configuration failed", StringComparison.Ordinal)));
    }

    private static TemporaryDirectory CreatePluginDirectory(params Assembly[] assemblies)
    {
        var directory = TemporaryDirectory.Create("roslyn-workbench-mcp-plugin-tests");
        var searchRoot = directory.DirectoryPath;

        for (var index = 0; index < assemblies.Length; index++)
        {
            var assembly = assemblies[index];
            var packageName = assembly.GetName().Name ?? "plugin";
            var packageDirectory = Path.Combine(searchRoot, $"{index:D2}-{packageName}");
            Directory.CreateDirectory(packageDirectory);
            if (assembly == typeof(HostValidQueryPlugin).Assembly)
            {
                var fixtureAssets = Path.Combine(AppContext.BaseDirectory, "PluginFixtureAssets", "HostQuery");
                foreach (var assetPath in Directory.EnumerateFiles(fixtureAssets))
                {
                    File.Copy(assetPath, Path.Combine(packageDirectory, Path.GetFileName(assetPath)), overwrite: true);
                }
            }
            else
            {
                File.Copy(assembly.Location, Path.Combine(packageDirectory, Path.GetFileName(assembly.Location)), overwrite: true);
            }
        }

        return directory;
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
