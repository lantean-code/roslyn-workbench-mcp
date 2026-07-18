namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCatalogEntryMaterializer : IPluginCatalogEntryMaterializer
{
    private readonly IPluginToolRegistrationMaterializer _toolRegistrationMaterializer;

    public PluginCatalogEntryMaterializer(IPluginToolRegistrationMaterializer toolRegistrationMaterializer)
    {
        _toolRegistrationMaterializer = toolRegistrationMaterializer;
    }

    public PluginCatalogEntryMaterialization Materialize(PreparedCatalogPlugin plugin)
    {
        try
        {
            var result = _toolRegistrationMaterializer.Materialize(plugin.Preparation);
            var diagnostics = CreateDiagnostics(result);
            return new PluginCatalogEntryMaterialization
            {
                Tools = result.Tools,
                Status = PluginCatalogStatusFactory.CreateEnabled(plugin.Metadata, diagnostics),
            };
        }
        catch (Exception exception)
        {
            return new PluginCatalogEntryMaterialization
            {
                Status = PluginCatalogStatusFactory.CreateDisabled(
                    plugin.Metadata,
                    PluginDiagnosticIds.Materialization,
                    $"Plugin tool materialization failed because {exception.GetType().Name} was raised."),
            };
        }
    }

    private static IReadOnlyList<DiagnosticInfo> CreateDiagnostics(PluginMaterializationResult result)
    {
        return result.Diagnostics
            .Concat(result.Tools.SelectMany(static tool => QueryResponseContractInspector.Inspect(tool.Tool)))
            .ToArray();
    }
}
