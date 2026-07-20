using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Tools;

internal abstract class ServerOwnedToolBase<TRequest, TResponse> : McpServerTool
    where TRequest : class
{
    private readonly Tool _protocolTool;

    protected ServerOwnedToolBase(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary = null)
    {
        _protocolTool = protocolFactory.CreateServerOwnedTool<TRequest, TResponse>(
            name,
            title,
            description,
            readOnly,
            destructive,
            resultSummary,
            startupOptions.Value.ToolOutputSchemaMode);
    }

    public override Tool ProtocolTool => _protocolTool;

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> requestContext, CancellationToken cancellationToken)
    {
        using var totalPhase = WorkbenchPerformanceEventSource.Log.StartPhase(
            ProtocolTool.Name,
            WorkbenchPerformanceEventSource.ToolTotalPhase);

        var arguments = requestContext.Params.Arguments ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        TRequest request;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            ProtocolTool.Name,
            WorkbenchPerformanceEventSource.RequestBindingPhase))
        {
            request = ToolRequestBinder.Deserialize<TRequest>(arguments);
        }

        ToolResult<TResponse> result;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            ProtocolTool.Name,
            WorkbenchPerformanceEventSource.HandlerExecutionPhase))
        {
            result = await ExecuteAsync(request, cancellationToken);
        }

        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            ProtocolTool.Name,
            WorkbenchPerformanceEventSource.ResponseProjectionPhase))
        {
            return new CallToolResult
            {
                Content = [],
                StructuredContent = SerializeResult(result),
                IsError = result.Outcome.IsError(),
            };
        }
    }

    protected abstract ValueTask<ToolResult<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);

    private static JsonElement SerializeResult(ToolResult<TResponse> result)
    {
        if (result.Outcome.IsError())
        {
            return ToolResultEnvelopeSerializer.CreateFailure(result.Error, result.RequiredAction);
        }

        return ToolResultEnvelopeSerializer.CreateSuccess(result.Data);
    }
}
