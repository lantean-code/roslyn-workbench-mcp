using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCatalogEntryMaterializer : IPluginCatalogEntryMaterializer
{
    private readonly IPluginToolRegistrationMaterializer _toolRegistrationMaterializer;

    public PluginCatalogEntryMaterializer(IPluginToolRegistrationMaterializer toolRegistrationMaterializer)
    {
        _toolRegistrationMaterializer = toolRegistrationMaterializer;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Tool materialization executes third-party plugin configuration; any plugin-defined failure must disable only that plugin and be reported through catalogue diagnostics.")]
    public PluginCatalogEntryMaterialization Materialize(PreparedCatalogPlugin plugin)
    {
        try
        {
            var result = _toolRegistrationMaterializer.Materialize(plugin.Preparation);
            var diagnostics = CreateDiagnostics(result);
            var status = PluginCatalogStatusFactory.CreateEnabled(plugin.Metadata, diagnostics);

            return new PluginCatalogEntryMaterialization
            {
                Tools = result.Tools,
                Status = status,
            };
        }
        catch (Exception exception)
        {
            var status = PluginCatalogStatusFactory.CreateDisabled(
                plugin.Metadata,
                PluginDiagnosticIds.Materialization,
                $"Plugin tool materialization failed because {exception.GetType().Name} was raised.");

            return new PluginCatalogEntryMaterialization
            {
                Status = status,
            };
        }
    }

    private static DiagnosticInfo[] CreateDiagnostics(PluginMaterializationResult result)
    {
        var diagnostics = new List<DiagnosticInfo>(result.Diagnostics);
        foreach (var tool in result.Tools)
        {
            diagnostics.AddRange(QueryResponseContractInspector.Inspect(tool.Tool));
        }

        return diagnostics.ToArray();
    }
}
