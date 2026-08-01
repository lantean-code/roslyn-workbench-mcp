using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal abstract class McpServerToolBase<TRequest> : McpServerTool
    where TRequest : class
{
    private readonly Tool _protocolTool;
    private readonly IToolRequestBinder _requestBinder;

    protected McpServerToolBase(Tool protocolTool, IToolRequestBinder requestBinder)
    {
        _protocolTool = protocolTool;
        _requestBinder = requestBinder;
    }

    public override Tool ProtocolTool => _protocolTool;

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        var arguments = requestContext.Params.Arguments
            ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return await InvokeArgumentsAsync(arguments, requestContext, cancellationToken);
    }

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

    protected PerformanceTraceScope StartPhase(string phase)
    {
        return WorkbenchPerformanceEventSource.Log.StartPhase(ProtocolTool.Name, phase);
    }

    protected abstract ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        CancellationToken cancellationToken);

    protected virtual ValueTask<CallToolResult> InvokeBoundRequestAsync(
        TRequest request,
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        return InvokeBoundRequestAsync(request, cancellationToken);
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
