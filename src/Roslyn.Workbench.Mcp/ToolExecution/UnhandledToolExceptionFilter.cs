using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Roslyn.Workbench.Mcp.Hosting;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed partial class UnhandledToolExceptionFilter
{
    private readonly ILogger<UnhandledToolExceptionFilter> _logger;
    private readonly IErrorCaptureService _captureService;
    private readonly ICapturedErrorStore _capturedErrorStore;
    private readonly IErrorReportingAvailabilityService _availabilityService;

    public UnhandledToolExceptionFilter(
        ILogger<UnhandledToolExceptionFilter> logger,
        IErrorCaptureService captureService,
        ICapturedErrorStore capturedErrorStore,
        IErrorReportingAvailabilityService availabilityService)
    {
        _logger = logger;
        _captureService = captureService;
        _capturedErrorStore = capturedErrorStore;
        _availabilityService = availabilityService;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "This is the top-level MCP tool boundary; unexpected failures other than request cancellation are logged and converted to a correlated error envelope, while deliberate Host routing protocol errors pass through.")]
    public async ValueTask<CallToolResult> InvokeAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            return await next(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RoslynWorkbenchMcpProtocolException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var correlationId = Guid.NewGuid();
            LogUnhandledToolException(
                _logger,
                context.Params.Name,
                correlationId,
                exception);

            var duration = Stopwatch.GetElapsedTime(startedAt);
            var record = _captureService.Capture(
                correlationId,
                context.Params.Name,
                context.Params.Arguments,
                duration,
                cancellationToken.IsCancellationRequested,
                exception);
            _capturedErrorStore.Add(record);

            bool? supportsElicitation = null;
            var clientCapabilities = context.Server.ClientCapabilities;
            if (clientCapabilities is not null)
            {
                supportsElicitation = clientCapabilities.Elicitation is not null;
            }

            var availability = _availabilityService.GetAvailability(
                record.Workspace?.WorkspaceId,
                record.Workspace?.WorkspaceEpoch,
                supportsElicitation);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = ToolResultEnvelopeSerializer.CreateUnhandledException(
                    correlationId,
                    availability),
                IsError = true,
            };
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Unhandled exception while executing MCP tool {ToolName}. Correlation ID: {CorrelationId}")]
    private static partial void LogUnhandledToolException(
        ILogger logger,
        string toolName,
        Guid correlationId,
        Exception exception);
}
