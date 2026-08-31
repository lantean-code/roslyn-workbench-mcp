using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Preflights transport schemas and creates runtime tool wrappers and an isolated service provider for one plugin.
/// </summary>
internal sealed partial class PluginCatalogEntryMaterializer : IPluginCatalogEntryMaterializer
{
    private const string _inputSchemaSizeRuleId = "InputSchemaSize";
    private const string _queryResponseContractRuleId = "RWMCP014";

    private readonly IPluginToolRegistrationMaterializer _toolRegistrationMaterializer;
    private readonly IPluginTransportSchemaPreflight _schemaPreflight;
    private readonly IToolSchemaFactory _schemaFactory;
    private readonly ILogger<PluginCatalogEntryMaterializer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginCatalogEntryMaterializer"/> class.
    /// </summary>
    /// <param name="toolRegistrationMaterializer">The component that builds runtime wrappers and the plugin service provider.</param>
    /// <param name="schemaPreflight">The validator that checks plugin tool schemas before publication.</param>
    /// <param name="schemaFactory">The factory used to measure published input schemas.</param>
    /// <param name="logger">The logger used to record diagnostic information.</param>
    public PluginCatalogEntryMaterializer(
        IPluginToolRegistrationMaterializer toolRegistrationMaterializer,
        IPluginTransportSchemaPreflight schemaPreflight,
        IToolSchemaFactory schemaFactory,
        ILogger<PluginCatalogEntryMaterializer> logger)
    {
        _toolRegistrationMaterializer = toolRegistrationMaterializer;
        _schemaPreflight = schemaPreflight;
        _schemaFactory = schemaFactory;
        _logger = logger;
    }

    /// <summary>
    /// Materializes one prepared plugin for runtime catalogue publication.
    /// </summary>
    /// <param name="plugin">The plugin instance being registered or inspected.</param>
    /// <returns>The published tools and enabled status, or a disabled status when materialization fails.</returns>
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
            ReportInputSchemaSizeWarnings(plugin.Metadata.PluginId, materialization.Tools);
            ReportQueryResponseContractWarnings(plugin.Metadata.PluginId, materialization.Tools);
            var status = PluginCatalogStatusFactory.CreateEnabled(plugin.Metadata, materialization.Diagnostics);

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

    private void ReportQueryResponseContractWarnings(
        string pluginId,
        IReadOnlyList<IRegisteredPluginTool> tools)
    {
        foreach (var tool in tools)
        {
            var registeredTool = tool.Tool;
            var warning = QueryResponseContractInspector.Inspect(registeredTool);
            if (warning is not null)
            {
                LogQueryResponseContractWarning(
                    _logger,
                    _queryResponseContractRuleId,
                    pluginId,
                    registeredTool.Metadata.Name,
                    warning);
            }
        }
    }

    private void ReportInputSchemaSizeWarnings(
        string pluginId,
        IReadOnlyList<IRegisteredPluginTool> tools)
    {
        foreach (var tool in tools)
        {
            var registeredTool = tool.Tool;
            var schema = _schemaFactory.CreateInputSchemaForType(registeredTool.RequestType);
            var sizeInBytes = InputSchemaBudget.GetSizeInBytes(schema);
            if (sizeInBytes <= InputSchemaBudget.MaximumSizeInBytes)
            {
                continue;
            }

            var formattedLimit = InputSchemaBudget.MaximumSizeInBytes.ToString("N0", CultureInfo.InvariantCulture);
            var warning = $"Request '{registeredTool.RequestType.Name}' publishes a {sizeInBytes}-byte input schema. "
                + $"Keep agent-facing input schemas at or below {formattedLimit} bytes so property guidance remains portable across MCP clients.";

            LogInputSchemaSizeWarning(
                _logger,
                _inputSchemaSizeRuleId,
                pluginId,
                registeredTool.Metadata.Name,
                warning);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Plugin authoring warning {RuleId} for plugin {PluginId}, tool {ToolName}: {WarningMessage}")]
    private static partial void LogQueryResponseContractWarning(
        ILogger logger,
        string ruleId,
        string pluginId,
        string toolName,
        string warningMessage);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Plugin authoring warning {RuleId} for plugin {PluginId}, tool {ToolName}: {WarningMessage}")]
    private static partial void LogInputSchemaSizeWarning(
        ILogger logger,
        string ruleId,
        string pluginId,
        string toolName,
        string warningMessage);
}
