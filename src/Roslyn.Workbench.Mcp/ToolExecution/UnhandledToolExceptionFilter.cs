namespace Roslyn.Workbench.Mcp.ToolExecution;

internal sealed class UnhandledToolExceptionFilter
{
    private readonly ILogger<UnhandledToolExceptionFilter> _logger;

    public UnhandledToolExceptionFilter(ILogger<UnhandledToolExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<CallToolResult> InvokeAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var correlationId = Guid.NewGuid().ToString("n");
            _logger.LogError(
                exception,
                "Unhandled exception while executing MCP tool {ToolName}. Correlation ID: {CorrelationId}",
                context.Params.Name,
                correlationId);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = ToolResultEnvelopeSerializer.CreateUnhandledException(correlationId),
                IsError = true,
            };
        }
    }
}
