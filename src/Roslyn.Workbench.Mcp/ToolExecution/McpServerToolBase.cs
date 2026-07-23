using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal abstract class McpServerToolBase : McpServerTool
{
    private readonly Tool _protocolTool;

    protected McpServerToolBase(Tool protocolTool)
    {
        _protocolTool = protocolTool;
    }

    public override Tool ProtocolTool => _protocolTool;

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        var arguments = requestContext.Params.Arguments
            ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return await InvokeArgumentsAsync(arguments, cancellationToken);
    }

    internal async ValueTask<CallToolResult> InvokeArgumentsAsync(
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        using var phase = StartPhase(WorkbenchPerformanceEventSource.ToolTotalPhase);
        return await InvokeCoreAsync(arguments, cancellationToken);
    }

    protected PerformanceTraceScope StartPhase(string phase)
    {
        return WorkbenchPerformanceEventSource.Log.StartPhase(ProtocolTool.Name, phase);
    }

    protected abstract ValueTask<CallToolResult> InvokeCoreAsync(
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken);

    protected static bool TryBindRequest<TRequest>(
        IDictionary<string, JsonElement> arguments,
        [NotNullWhen(true)] out TRequest? request,
        [NotNullWhen(false)] out CallToolResult? rejection)
        where TRequest : class
    {
        try
        {
            request = ToolRequestBinder.Deserialize<TRequest>(arguments);
            rejection = null;
            return true;
        }
        catch (JsonException exception)
        {
            request = null;
            var error = new ToolError
            {
                Code = "InvalidRequest",
                Message = $"The tool arguments did not match the request contract. {exception.Message}",
            };

            var content = ToolResultEnvelopeSerializer.CreateFailure(error, requiredAction: null);
            rejection = CreateStructuredResult(content, isError: true);
            return false;
        }
    }

    protected static CallToolResult CreateStructuredResult(JsonElement content, bool isError)
    {
        return new CallToolResult
        {
            Content = [],
            StructuredContent = content,
            IsError = isError,
        };
    }
}
