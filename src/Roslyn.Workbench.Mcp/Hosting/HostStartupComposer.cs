using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed class HostStartupComposer
{
    private readonly IPluginCatalogBootstrap _pluginCatalogBootstrap;
    private readonly IWorkspacePathComparison _pathComparison;

    public HostStartupComposer(
        IPluginCatalogBootstrap pluginCatalogBootstrap,
        IWorkspacePathComparison pathComparison)
    {
        _pluginCatalogBootstrap = pluginCatalogBootstrap;
        _pathComparison = pathComparison;
    }

    public HostStartupComposition Compose(string[] args)
    {
        var configuration = StartupOptionsResolver.Resolve(args, _pathComparison);
        var optionsValidator = new StartupOptionsValidator();
        optionsValidator.EnsureValid(configuration.Options);

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
