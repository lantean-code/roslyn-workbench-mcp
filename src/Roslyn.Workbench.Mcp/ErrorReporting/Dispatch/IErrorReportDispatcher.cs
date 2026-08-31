namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Prepares provider-specific previews and dispatches approved error reports.
/// </summary>
internal interface IErrorReportDispatcher
{
    /// <summary>
    /// Gets the provider name shown during review and in submission results.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the external or local destination shown to the user before approval.
    /// </summary>
    string Destination { get; }

    /// <summary>
    /// Creates the immutable provider-specific payload presented for review.
    /// </summary>
    /// <param name="report">The projected error report to encode for this provider.</param>
    /// <returns>The provider-specific payload, preview and dispatch state.</returns>
    PreparedDispatchPayload CreatePayload(ExternalErrorReport report);

    /// <summary>
    /// Applies the approved exception-message policy and sends the prepared report to its destination.
    /// </summary>
    /// <param name="payload">The prepared payload approved for dispatch.</param>
    /// <param name="messageHandling">Whether captured exception messages are included or removed before dispatch.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the error dispatch result.</returns>
    ValueTask<ErrorDispatchResult> DispatchAsync(
        PreparedDispatchPayload payload,
        ExceptionMessageHandling messageHandling,
        CancellationToken cancellationToken);
}
