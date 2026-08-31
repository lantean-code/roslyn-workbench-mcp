using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Generates every plugin tool schema before publication and rejects unsupported transport contracts.
/// </summary>
internal sealed class PluginTransportSchemaPreflight : IPluginTransportSchemaPreflight
{
    private readonly IToolSchemaFactory _schemaFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginTransportSchemaPreflight"/> class.
    /// </summary>
    /// <param name="schemaFactory">The factory used to generate MCP input and output schemas.</param>
    public PluginTransportSchemaPreflight(IToolSchemaFactory schemaFactory)
    {
        _schemaFactory = schemaFactory;
    }

    /// <summary>
    /// Validates plugin transport schemas before publication.
    /// </summary>
    /// <param name="tools">The tools whose published transport schemas must be validated.</param>
    /// <returns>Success when every schema is publishable; otherwise, diagnostics for each rejected contract.</returns>
    public PluginTransportSchemaPreflightResult Preflight(IReadOnlyList<PreparedPluginTool> tools)
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

            var outputFailure = TryValidateOutputContract(tool);
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
    private DiagnosticInfo? TryValidateOutputContract(RegisteredTool tool)
    {
        try
        {
            var kind = tool.Kind == ToolKind.Query
                ? PublishedToolKind.Query
                : PublishedToolKind.Mutation;

            _schemaFactory.CreateOutputSchema(kind, tool.ResponseType);

            if (tool.Kind == ToolKind.Query)
            {
                var contractKind = ToolResultEnvelopeSerializer.GetSuccessDataContractKind(tool.ResponseType);
                if (contractKind != JsonTypeInfoKind.Object)
                {
                    return CreateFailure(tool, "response", tool.ResponseType, contractKind);
                }
            }

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
        JsonTypeInfoKind contractKind)
    {
        var contractTypeName = contractType.FullName ?? contractType.Name;
        var message = $"Tool '{tool.Metadata.Name}' {contractDirection} contract '{contractTypeName}' "
            + $"cannot be admitted because its JSON contract kind is '{contractKind}' rather than an object.";

        return PluginCatalogStatusFactory.CreateDiagnostic(
            PluginDiagnosticIds.ToolSchema,
            DiagnosticSeverity.Error,
            message);
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
