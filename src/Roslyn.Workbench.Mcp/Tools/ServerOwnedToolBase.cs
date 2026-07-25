using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution;

namespace Roslyn.Workbench.Mcp.Tools;

internal abstract class ServerOwnedToolBase<TRequest, TResponse> : McpServerToolBase<TRequest>
    where TRequest : class
{
    protected ServerOwnedToolBase(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary = null)
        : base(protocolFactory.CreateServerOwnedTool<TRequest, TResponse>(
            name,
            title,
            description,
            readOnly,
            destructive,
            resultSummary,
            startupOptions.Value.ToolOutputSchemaMode))
    {
    }

    protected abstract ValueTask<ToolResult<TResponse>> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken);

    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        ToolResult<TResponse> result;
        using (StartPhase(WorkbenchPerformanceEventSource.HandlerExecutionPhase))
        {
            result = await ExecuteAsync(request, cancellationToken);
        }

        using (StartPhase(WorkbenchPerformanceEventSource.ResponseProjectionPhase))
        {
            var content = SerializeResult(result);
            return CreateStructuredResult(content, result.Outcome.IsError());
        }
    }

    private static JsonElement SerializeResult(ToolResult<TResponse> result)
    {
        if (result.Outcome.IsError())
        {
            return ToolResultEnvelopeSerializer.CreateFailure(result.Error, result.RequiredAction);
        }

        return ToolResultEnvelopeSerializer.CreateSuccess(result.Data);
    }
}
