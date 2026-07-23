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
        var pathComparison = new WorkspacePathComparison();
        var packagePathPolicy = new PluginPackagePathPolicy(fileSystem, pathComparison);
        var metadataReader = new PluginAssemblyMetadataReader(fileSystem);

        var handlerTypeInspector = new PluginHandlerTypeInspector();
        var handlerContractResolver = new PluginHandlerContractResolver();
        var handlerWarningInspector = new PluginHandlerWarningInspector();
        var configurationPreparer = new PluginConfigurationPreparer(
            handlerTypeInspector,
            handlerContractResolver,
            handlerWarningInspector);

        var toolRegistrationMaterializer = new PluginToolRegistrationMaterializer();
        var pluginComposer = new MefPluginComposer();
        var loadedPluginPreparer = new LoadedPluginPreparer(pluginComposer, configurationPreparer);

        var entryPointValidator = new PluginEntryPointValidator();
        var loadContextFactory = new PluginLoadContextFactory(packagePathPolicy);
        var candidatePreparer = new PluginCandidatePreparer(
            metadataReader,
            entryPointValidator,
            loadedPluginPreparer,
            loadContextFactory);

        var entryMaterializer = new PluginCatalogEntryMaterializer(toolRegistrationMaterializer);
        var packageDiscovery = new PluginPackageDiscovery(fileSystem, metadataReader, packagePathPolicy);
        var collisionPolicy = new PluginCollisionPolicy();
        var loader = new PluginCatalogLoader(
            candidatePreparer,
            entryMaterializer,
            collisionPolicy,
            packageDiscovery);

        return loader.Load(startupOptions, bundledAssemblies, reservedToolNames);
    }
}
