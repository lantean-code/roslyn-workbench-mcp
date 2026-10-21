using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ModelContextProtocol;

namespace Roslyn.Workbench.Mcp.ToolExecution;

/// <summary>
/// Retains unexpected resource-read failures and exposes correlated, generic JSON-RPC diagnostics.
/// </summary>
internal sealed partial class UnhandledResourceExceptionFilter
{
    private readonly ILogger<UnhandledResourceExceptionFilter> _logger;
    private readonly IErrorCaptureService _captureService;
    private readonly ICapturedErrorStore _capturedErrorStore;
    private readonly IErrorReportingAvailabilityService _availabilityService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnhandledResourceExceptionFilter"/> class.
    /// </summary>
    /// <param name="logger">The local diagnostic logger.</param>
    /// <param name="captureService">The bounded diagnostic capture service.</param>
    /// <param name="capturedErrorStore">The temporary diagnostic store.</param>
    /// <param name="availabilityService">The reporting policy and client availability service.</param>
    public UnhandledResourceExceptionFilter(
        ILogger<UnhandledResourceExceptionFilter> logger,
        IErrorCaptureService captureService,
        ICapturedErrorStore capturedErrorStore,
        IErrorReportingAvailabilityService availabilityService)
    {
        _logger = logger;
        _captureService = captureService;
        _capturedErrorStore = capturedErrorStore;
        _availabilityService = availabilityService;
    }

    /// <summary>
    /// Invokes the resource handler and retains unexpected failures while preserving request cancellation and deliberate protocol errors.
    /// </summary>
    /// <param name="next">The next resource-read handler.</param>
    /// <param name="context">The resource request context.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The unchanged successful resource result.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The resource-read boundary captures unexpected failures and returns generic correlated protocol diagnostics.")]
    public async ValueTask<ReadResourceResult> InvokeAsync(
        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> next,
        RequestContext<ReadResourceRequestParams> context,
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
        catch (McpProtocolException exception) when (context.MatchedPrimitive is null && exception.ErrorCode == McpErrorCode.ResourceNotFound)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CaptureFailure(context, startedAt, exception, cancellationToken);
        }
    }

    private McpProtocolException CaptureFailure(
        RequestContext<ReadResourceRequestParams> context,
        long startedAt,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        LogUnhandledResourceException(_logger, correlationId, exception);
        var duration = Stopwatch.GetElapsedTime(startedAt);
        var record = _captureService.CaptureResourceRead(correlationId, duration, cancellationToken.IsCancellationRequested, exception);
        _capturedErrorStore.Add(record);

        bool? supportsElicitation = null;
        var clientCapabilities = context.Server.ClientCapabilities;
        if (clientCapabilities is not null)
        {
            supportsElicitation = clientCapabilities.Elicitation is not null;
        }

        var availability = _availabilityService.GetAvailability(null, null, supportsElicitation);
        var payload = ToolResultEnvelopeSerializer.CreateUnhandledException(correlationId, availability, "Resource read failed.");
        var failure = new McpProtocolException($"Resource read failed. Correlation ID: {correlationId}.", McpErrorCode.InternalError);
        foreach (var property in payload.EnumerateObject())
        {
            failure.Data[property.Name] = property.Value.Clone();
        }

        return failure;
    }

    [LoggerMessage(1, LogLevel.Error, "Unhandled exception while reading an MCP resource. Correlation ID: {CorrelationId}")]
    private static partial void LogUnhandledResourceException(ILogger logger, Guid correlationId, Exception exception);
}
