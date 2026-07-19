using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed partial class UnhandledToolExceptionFilter
{
    private readonly ILogger<UnhandledToolExceptionFilter> _logger;

    public UnhandledToolExceptionFilter(ILogger<UnhandledToolExceptionFilter> logger)
    {
        _logger = logger;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "This is the top-level MCP tool boundary; unexpected non-cancellation failures are logged and converted to a correlated error envelope so they do not terminate the server.")]
    public async ValueTask<CallToolResult> InvokeAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var correlationId = Guid.NewGuid().ToString("n");
            LogUnhandledToolException(
                _logger,
                context.Params.Name,
                correlationId,
                exception);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = ToolResultEnvelopeSerializer.CreateUnhandledException(correlationId),
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
        string correlationId,
        Exception exception);
}
