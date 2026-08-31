using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution;

/// <summary>
/// Provides request binding, validation, and performance tracing for MCP server tools.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
internal abstract class McpServerToolBase<TRequest> : McpServerTool
    where TRequest : class
{
    private readonly Tool _protocolTool;
    private readonly IToolRequestBinder _requestBinder;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServerToolBase{TRequest}"/> class.
    /// </summary>
    /// <param name="protocolTool">The protocol-layer tool whose execution is exposed through MCP.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    protected McpServerToolBase(Tool protocolTool, IToolRequestBinder requestBinder)
    {
        _protocolTool = protocolTool;
        _requestBinder = requestBinder;
    }

    /// <inheritdoc/>
    public override Tool ProtocolTool => _protocolTool;

    /// <inheritdoc/>
    public override IReadOnlyList<object> Metadata => [];

    /// <inheritdoc/>
    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        var arguments = requestContext.Params.Arguments
            ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return await InvokeArgumentsAsync(arguments, requestContext, cancellationToken);
    }

    /// <summary>
    /// Binds supplied arguments and invokes the resulting request without an MCP request context.
    /// </summary>
    /// <param name="arguments">The arguments supplied to the tool invocation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The tool result produced by the bound request, or an invalid-request result when binding fails.</returns>
    internal async ValueTask<CallToolResult> InvokeArgumentsAsync(
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        return await InvokeArgumentsAsync(
            arguments,
            requestContext: null,
            cancellationToken);
    }

    private async ValueTask<CallToolResult> InvokeArgumentsAsync(
        IDictionary<string, JsonElement> arguments,
        RequestContext<CallToolRequestParams>? requestContext,
        CancellationToken cancellationToken)
    {
        using var phase = StartPhase(WorkbenchPerformanceEventSource.ToolTotalPhase);

        TRequest request;
        using (StartPhase(WorkbenchPerformanceEventSource.RequestBindingPhase))
        {
            if (!TryBindRequest(arguments, out var boundRequest, out var rejection))
            {
                return rejection;
            }

            request = boundRequest;
        }

        return requestContext is null
            ? await InvokeBoundRequestAsync(request, cancellationToken)
            : await InvokeBoundRequestAsync(request, requestContext, cancellationToken);
    }

    /// <summary>
    /// Starts performance measurement for one tool-execution phase.
    /// </summary>
    /// <param name="phase">The tool execution phase to record in diagnostics and error reports.</param>
    /// <returns>The performance trace scope.</returns>
    protected PerformanceTraceScope StartPhase(string phase)
    {
        return WorkbenchPerformanceEventSource.Log.StartPhase(ProtocolTool.Name, phase);
    }

    /// <summary>
    /// Executes a request that has already passed transport binding and validation.
    /// </summary>
    /// <param name="request">The validated tool request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The result of executing the request.</returns>
    protected abstract ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes a validated request with access to its MCP request context.
    /// </summary>
    /// <param name="request">The validated tool request.</param>
    /// <param name="requestContext">The MCP request context supplied to the bound tool invocation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The result of executing the request.</returns>
    protected virtual ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        return InvokeBoundRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Creates a call result with matching textual and structured JSON content.
    /// </summary>
    /// <param name="content">The JSON payload included in both result representations.</param>
    /// <param name="isError">Whether the protocol result represents an error.</param>
    /// <returns>The call tool result.</returns>
    protected static CallToolResult CreateStructuredResult(JsonElement content, bool isError)
    {
        return CallToolResultFactory.CreateStructured(content, isError);
    }

    private bool TryBindRequest(
        IDictionary<string, JsonElement> arguments,
        [NotNullWhen(true)] out TRequest? request,
        [NotNullWhen(false)] out CallToolResult? rejection)
    {
        if (!_requestBinder.TryBind(arguments, out request, out var errorMessage))
        {
            rejection = CreateBindingRejection(errorMessage);
            return false;
        }

        rejection = null;
        return true;
    }

    private static CallToolResult CreateBindingRejection(string message)
    {
        var error = new ToolError
        {
            Code = "InvalidRequest",
            Message = message,
        };

        var content = ToolResultEnvelopeSerializer.CreateFailure(error, requiredAction: null);
        return CreateStructuredResult(content, isError: true);
    }
}
