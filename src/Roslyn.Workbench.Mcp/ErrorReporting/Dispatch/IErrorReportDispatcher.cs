namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal interface IErrorReportDispatcher
{
    string Name { get; }

    string Destination { get; }

    PreparedDispatchPayload CreatePayload(ExternalErrorReport report);

    ValueTask<ErrorDispatchResult> DispatchAsync(
        PreparedDispatchPayload payload,
        ExceptionMessageHandling messageHandling,
        CancellationToken cancellationToken);
}
