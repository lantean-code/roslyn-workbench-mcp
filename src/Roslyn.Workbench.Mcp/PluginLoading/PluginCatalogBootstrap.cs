using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCatalogBootstrap : IPluginCatalogBootstrap
{
    public PluginCatalogSnapshot Load(
        StartupOptions startupOptions,
        IReadOnlyList<Assembly> bundledAssemblies,
        IEnumerable<string>? reservedToolNames = null)
    {
        var fileSystem = new FileSystem();
        var packagePathPolicy = new PluginPackagePathPolicy(fileSystem, new WorkspacePathComparison());
        var metadataReader = new PluginAssemblyMetadataReader(fileSystem);
        var configurationPreparer = new PluginConfigurationPreparer(
            new PluginHandlerTypeInspector(),
            new PluginHandlerContractResolver(),
            new PluginHandlerWarningInspector());

        var toolRegistrationMaterializer = new PluginToolRegistrationMaterializer();
        var loadedPluginPreparer = new LoadedPluginPreparer(
            new MefPluginComposer(),
            configurationPreparer);

        var candidatePreparer = new PluginCandidatePreparer(
            metadataReader,
            new PluginEntryPointValidator(),
            loadedPluginPreparer,
            new PluginLoadContextFactory(packagePathPolicy));

        var entryMaterializer = new PluginCatalogEntryMaterializer(toolRegistrationMaterializer);
        var packageDiscovery = new PluginPackageDiscovery(fileSystem, metadataReader, packagePathPolicy);

        var loader = new PluginCatalogLoader(
            candidatePreparer,
            entryMaterializer,
            new PluginCollisionPolicy(),
            packageDiscovery);

        return loader.Load(startupOptions, bundledAssemblies, reservedToolNames);
    }
}
