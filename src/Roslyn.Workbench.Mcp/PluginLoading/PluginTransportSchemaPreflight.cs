using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginTransportSchemaPreflight : IPluginTransportSchemaPreflight
{
    private readonly IToolSchemaFactory _schemaFactory;

    public PluginTransportSchemaPreflight(IToolSchemaFactory schemaFactory)
    {
        _schemaFactory = schemaFactory;
    }

    public PluginTransportSchemaPreflightResult Preflight(
        IReadOnlyList<PreparedPluginTool> tools,
        ToolOutputSchemaMode outputSchemaMode)
    {
        var failures = new List<DiagnosticInfo>();
        foreach (var preparedTool in tools)
        {
            var tool = preparedTool.Tool;
            var inputFailure = TryCreateInputSchema(tool);
            if (inputFailure is not null)
            {
                failures.Add(inputFailure);
            }

            if (outputSchemaMode != ToolOutputSchemaMode.Full)
            {
                continue;
            }

            var outputFailure = TryCreateOutputSchema(tool);
            if (outputFailure is not null)
            {
                failures.Add(outputFailure);
            }
        }

        return failures.Count == 0
            ? PluginTransportSchemaPreflightResult.Success()
            : PluginTransportSchemaPreflightResult.Failure(failures);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Schema generation inspects third-party contracts; any provider failure must disable only the owning plugin and become a catalogue diagnostic.")]
    private DiagnosticInfo? TryCreateInputSchema(RegisteredTool tool)
    {
        try
        {
            _schemaFactory.CreateInputSchemaForType(tool.RequestType);
            return null;
        }
        catch (Exception exception)
        {
            return CreateFailure(tool, "request", tool.RequestType, exception);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Schema generation inspects third-party contracts; any provider failure must disable only the owning plugin and become a catalogue diagnostic.")]
    private DiagnosticInfo? TryCreateOutputSchema(RegisteredTool tool)
    {
        try
        {
            var kind = tool.Kind == ToolKind.Query
                ? PublishedToolKind.Query
                : PublishedToolKind.Mutation;

            _schemaFactory.CreateOutputSchema(kind, tool.ResponseType);
            return null;
        }
        catch (Exception exception)
        {
            return CreateFailure(tool, "response", tool.ResponseType, exception);
        }
    }

    private static DiagnosticInfo CreateFailure(
        RegisteredTool tool,
        string contractDirection,
        Type contractType,
        Exception exception)
    {
        var rootException = exception.GetBaseException();
        var contractTypeName = contractType.FullName ?? contractType.Name;
        var exceptionTypeName = rootException.GetType().Name;
        var message = $"Tool '{tool.Metadata.Name}' {contractDirection} contract '{contractTypeName}' "
            + $"could not be represented as an MCP schema because {exceptionTypeName} was raised.";

        return PluginCatalogStatusFactory.CreateDiagnostic(
            PluginDiagnosticIds.ToolSchema,
            DiagnosticSeverity.Error,
            message);
    }
}
