namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class PluginToolCatalogueFactory
{
    public static PluginToolCatalogue CreateMutationTool<THandler>(
        PluginMetadata pluginMetadata,
        string name,
        string title,
        string description,
        bool destructive = false)
        where THandler : class, IMutationToolHandler, new()
    {
        var configuration = new PluginConfiguration();
        var builder = configuration.AddMutationTool<THandler>()
            .WithName(name)
            .WithTitle(title)
            .WithDescription(description)
            .IsDestructive(destructive);
        configuration.Freeze();

        var configurationPreparer = new PluginConfigurationPreparer(
            new PluginHandlerTypeInspector(),
            new PluginHandlerContractResolver(),
            new PluginHandlerWarningInspector());
        var preparation = configurationPreparer.Prepare(pluginMetadata, configuration);
        var materialization = new PluginToolRegistrationMaterializer().Materialize(preparation);
        return new PluginToolCatalogue(materialization.Tools);
    }
}
