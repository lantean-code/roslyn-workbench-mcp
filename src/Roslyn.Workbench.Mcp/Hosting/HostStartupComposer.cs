using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed class HostStartupComposer
{
    private readonly IPluginCatalogBootstrap _pluginCatalogBootstrap;

    public HostStartupComposer(IPluginCatalogBootstrap pluginCatalogBootstrap)
    {
        _pluginCatalogBootstrap = pluginCatalogBootstrap;
    }

    public HostStartupComposition Compose(string[] args)
    {
        var configuration = StartupOptionsResolver.Resolve(args);
        new StartupOptionsValidator().EnsureValid(configuration.Options);
        var codeActions = new CodeActionCatalogSnapshot
        {
            Tools = BundledCodeActionCatalog.Create(),
        };

        var plugins = _pluginCatalogBootstrap.Load(
            configuration.Options,
            [typeof(BundledCorePlugin).Assembly],
            codeActions.Tools
                .Select(static tool => tool.Metadata.Name)
                .Concat(ServerOwnedToolRegistration.ToolNames));

        return new HostStartupComposition
        {
            Configuration = configuration,
            CodeActions = codeActions,
            Plugins = plugins,
        };
    }
}
