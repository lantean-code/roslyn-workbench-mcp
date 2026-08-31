using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution;

namespace Roslyn.Workbench.Mcp.Tools;

/// <summary>
/// Publishes and executes Host-owned tools using the common structured-result envelope.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal abstract class ServerOwnedToolBase<TRequest, TResponse> : McpServerToolBase<TRequest>
    where TRequest : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerOwnedToolBase{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates the published MCP tool definition.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="name">The protocol name used to invoke the tool.</param>
    /// <param name="title">The human-readable tool title.</param>
    /// <param name="description">The guidance published to MCP clients.</param>
    /// <param name="readOnly">Whether the operation is restricted to read-only behaviour.</param>
    /// <param name="destructive">Whether the operation may discard or overwrite data.</param>
    /// <param name="resultSummary">Optional guidance describing the structured result.</param>
    /// <param name="idempotent">Whether repeated invocations with the same input have the same effect.</param>
    /// <param name="openWorld">Whether the tool may interact with resources outside the current workspace.</param>
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

    /// <summary>
    /// Executes the tool request.
    /// </summary>
    /// <param name="request">The validated tool request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The structured result to publish for the request.</returns>
    protected abstract ValueTask<ToolResult<TResponse>> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken);

    /// <inheritdoc/>
    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        ToolResult<TResponse> result;
        using (StartPhase(WorkbenchPerformanceEventSource.HandlerExecutionPhase))
        {
            try
            {
                result = await ExecuteAsync(request, cancellationToken);
            }
            catch (WorkspaceOperationException exception)
                when (exception.InnerException is Exception failure)
            {
                var failureContext = exception.Context;
                var workspaceContext = new CapturedWorkspaceContext(
                    failureContext.Workspace,
                    failureContext.LifecycleState,
                    failureContext.ProjectCount,
                    failureContext.DocumentCount,
                    failureContext.TransactionRevision);

                throw new WorkspaceAttributedToolException(workspaceContext, failure);
            }
        }

        using (StartPhase(WorkbenchPerformanceEventSource.ResponseProjectionPhase))
        {
            var content = SerializeResult(result);
            return CreateStructuredResult(content, result.Outcome.IsError());
        }
    }

    /// <inheritdoc/>
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

        return ToolResultEnvelopeSerializer.CreateSuccess(result.Data, result.Snapshot);
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
