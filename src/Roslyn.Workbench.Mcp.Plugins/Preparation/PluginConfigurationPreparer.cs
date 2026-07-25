using ContractDiagnosticSeverity = Roslyn.Workbench.Mcp.Workspace.Results.DiagnosticSeverity;

namespace Roslyn.Workbench.Mcp.Plugins.Preparation;

internal sealed class PluginConfigurationPreparer : IPluginConfigurationPreparer
{
    private readonly IPluginHandlerContractResolver _contractResolver;
    private readonly IPluginHandlerTypeInspector _typeInspector;
    private readonly IPluginHandlerWarningInspector _warningInspector;

    public PluginConfigurationPreparer(
        IPluginHandlerTypeInspector typeInspector,
        IPluginHandlerContractResolver contractResolver,
        IPluginHandlerWarningInspector warningInspector)
    {
        _typeInspector = typeInspector;
        _contractResolver = contractResolver;
        _warningInspector = warningInspector;
    }

    public PluginPreparationResult Prepare(
        PluginMetadata pluginMetadata,
        PluginConfiguration configuration,
        PluginContractAccessibility contractAccessibility)
    {
        var tools = new List<PreparedPluginTool>();
        var diagnostics = new List<DiagnosticInfo>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in configuration.Definitions)
        {
            var diagnosticCount = diagnostics.Count;
            diagnostics.AddRange(_typeInspector.Inspect(definition.HandlerType));
            var hasContract = _contractResolver.TryResolve(
                definition,
                contractAccessibility,
                out var contract,
                out var contractDiagnostic);

            if (!hasContract && contractDiagnostic is not null)
            {
                diagnostics.Add(contractDiagnostic);
            }

            var metadata = PluginToolMetadataFactory.Create(definition);
            var hasMetadata = HasRequiredMetadata(metadata);
            if (!hasMetadata)
            {
                diagnostics.Add(CreateError(
                    PluginDiagnosticIds.ToolMetadata,
                    $"Tool handler '{definition.HandlerType.FullName}' metadata must provide Name, Title, and Description through RoslynToolAttribute or fluent configuration."));
            }

            if (definition.Kind == ToolKind.Query && metadata.Behavior.Destructive)
            {
                diagnostics.Add(CreateError(
                    PluginDiagnosticIds.ToolBehaviour,
                    $"Query handler '{definition.HandlerType.FullName}' cannot declare destructive behaviour."));
            }

            if (hasMetadata && !names.Add(metadata.Name))
            {
                diagnostics.Add(CreateError(
                    PluginDiagnosticIds.ToolName,
                    $"Tool name '{metadata.Name}' is configured more than once by plugin '{pluginMetadata.PluginId}'."));
            }

            diagnostics.AddRange(_warningInspector.Inspect(definition.HandlerType));
            if (hasContract && contract is not null && !HasErrors(diagnostics, diagnosticCount))
            {
                tools.Add(CreatePreparedTool(pluginMetadata, definition, contract, metadata));
            }
        }

        return new PluginPreparationResult
        {
            Tools = HasErrors(diagnostics, 0) ? [] : tools.ToArray(),
            Diagnostics = diagnostics.ToArray(),
        };
    }

    private static bool HasRequiredMetadata(ToolRegistrationMetadata metadata)
    {
        return !string.IsNullOrWhiteSpace(metadata.Name)
            && !string.IsNullOrWhiteSpace(metadata.Title)
            && !string.IsNullOrWhiteSpace(metadata.Description);
    }

    private static bool HasErrors(List<DiagnosticInfo> diagnostics, int startIndex)
    {
        for (var index = startIndex; index < diagnostics.Count; index++)
        {
            if (diagnostics[index].Severity == ContractDiagnosticSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }

    private static DiagnosticInfo CreateError(string id, string message)
    {
        return new DiagnosticInfo
        {
            Id = id,
            Severity = ContractDiagnosticSeverity.Error,
            Message = message,
        };
    }

    private static PreparedPluginTool CreatePreparedTool(
        PluginMetadata pluginMetadata,
        ConfiguredToolDefinition definition,
        Type contract,
        ToolRegistrationMetadata metadata)
    {
        return new PreparedPluginTool
        {
            HandlerType = definition.HandlerType,
            HandlerContract = contract,
            HandlerFactory = definition.HandlerFactory,
            Tool = new RegisteredTool
            {
                Plugin = pluginMetadata,
                Metadata = metadata,
                Kind = definition.Kind,
                RequestType = contract.GenericTypeArguments[0],
                ResponseType = definition.Kind == ToolKind.Query
                    ? contract.GenericTypeArguments[1]
                    : typeof(MutationData),
            },
        };
    }
}
