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
        IToolRequestBinder requestBinder,
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary = null,
        bool? idempotent = null,
        bool openWorld = false)
        : base(CreateProtocolTool(
            protocolFactory,
            startupOptions.Value.ToolOutputSchemaMode,
            name,
            title,
            description,
            readOnly,
            destructive,
            resultSummary,
            idempotent,
            openWorld),
            requestBinder)
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

    protected override ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        return InvokeBoundRequestAsync(request, cancellationToken);
    }

    private static JsonElement SerializeResult(ToolResult<TResponse> result)
    {
        if (result.Outcome.IsError())
        {
            return ToolResultEnvelopeSerializer.CreateFailure(
                result.Error,
                result.RequiredAction,
                result.Diagnostics,
                result.Warnings);
        }

        return ToolResultEnvelopeSerializer.CreateSuccess(result.Data);
    }

    private static Tool CreateProtocolTool(
        IMcpToolProtocolFactory protocolFactory,
        ToolOutputSchemaMode outputSchemaMode,
        string name,
        string title,
        string description,
        bool readOnly,
        bool destructive,
        string? resultSummary,
        bool? idempotent,
        bool openWorld)
    {
        if (idempotent is null && !openWorld)
        {
            return protocolFactory.CreateServerOwnedTool<TRequest, TResponse>(
                name,
                title,
                description,
                readOnly,
                destructive,
                resultSummary,
                outputSchemaMode);
        }

        return protocolFactory.CreateServerOwnedToolWithAnnotations<TRequest, TResponse>(
            name,
            title,
            description,
            readOnly,
            destructive,
            resultSummary,
            outputSchemaMode,
            idempotent ?? readOnly,
            openWorld);
    }
}
