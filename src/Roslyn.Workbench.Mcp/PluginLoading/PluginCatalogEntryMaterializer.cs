using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCatalogEntryMaterializer : IPluginCatalogEntryMaterializer
{
    private readonly IPluginToolRegistrationMaterializer _toolRegistrationMaterializer;
    private readonly IPluginTransportSchemaPreflight _schemaPreflight;

    public PluginCatalogEntryMaterializer(
        IPluginToolRegistrationMaterializer toolRegistrationMaterializer,
        IPluginTransportSchemaPreflight schemaPreflight)
    {
        _toolRegistrationMaterializer = toolRegistrationMaterializer;
        _schemaPreflight = schemaPreflight;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Tool materialization executes third-party plugin configuration; any plugin-defined failure must disable only that plugin and be reported through catalogue diagnostics.")]
    public PluginCatalogEntryMaterialization Materialize(PreparedCatalogPlugin plugin)
    {
        PluginMaterializationResult? materialization = null;
        try
        {
            var schemaPreflight = _schemaPreflight.Preflight(plugin.Preparation.Tools);
            if (!schemaPreflight.Succeeded)
            {
                var disabledStatus = PluginCatalogStatusFactory.CreateDisabled(plugin.Metadata, schemaPreflight.Failures);

                return new PluginCatalogEntryMaterialization
                {
                    Status = disabledStatus,
                };
            }

            materialization = _toolRegistrationMaterializer.Materialize(plugin.Preparation);
            var diagnostics = CreateDiagnostics(materialization);
            var status = PluginCatalogStatusFactory.CreateEnabled(plugin.Metadata, diagnostics);

            return new PluginCatalogEntryMaterialization
            {
                Tools = materialization.Tools,
                Status = status,
                ServiceProviderLifetime = materialization.ServiceProviderLifetime,
            };
        }
        catch (Exception exception)
        {
            var disposalException = TryDispose(materialization?.ServiceProviderLifetime);
            var failureMessage = CreateMaterializationFailureMessage(exception, disposalException);
            var status = PluginCatalogStatusFactory.CreateDisabled(
                plugin.Metadata,
                PluginDiagnosticIds.Materialization,
                failureMessage);

            return new PluginCatalogEntryMaterialization
            {
                Status = status,
            };
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A plugin-owned disposal failure must remain within the per-plugin materialization failure boundary.")]
    private static Exception? TryDispose(IDisposable? lifetime)
    {
        try
        {
            lifetime?.Dispose();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static string CreateMaterializationFailureMessage(
        Exception materializationException,
        Exception? disposalException)
    {
        var message = $"Plugin tool materialization failed because {materializationException.GetType().Name} was raised.";
        if (disposalException is null)
        {
            return message;
        }

        return $"{message} Plugin service cleanup also failed because {disposalException.GetType().Name} was raised.";
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
