namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Defines stable error codes emitted directly by Host-owned MCP tool paths.
/// </summary>
internal static class HostToolErrorCodes
{
    /// <summary>
    /// The connected client cannot complete a required approval interaction.
    /// </summary>
    public const string ApprovalUnavailable = "ApprovalUnavailable";

    /// <summary>
    /// The requested captured error details are unavailable.
    /// </summary>
    public const string ErrorDetailsUnavailable = "ErrorDetailsUnavailable";

    /// <summary>
    /// The prepared-report store has reached its configured capacity.
    /// </summary>
    public const string ErrorReportCapacityReached = "ErrorReportCapacityReached";

    /// <summary>
    /// The user declined submission of the prepared error report.
    /// </summary>
    public const string ErrorReportNotApproved = "ErrorReportNotApproved";

    /// <summary>
    /// The prepared error report exceeds its configured payload limit.
    /// </summary>
    public const string ErrorReportPayloadTooLarge = "ErrorReportPayloadTooLarge";

    /// <summary>
    /// The prepared error report is already being submitted.
    /// </summary>
    public const string ErrorReportSubmissionInProgress = "ErrorReportSubmissionInProgress";

    /// <summary>
    /// Error reporting is unavailable under the effective configuration.
    /// </summary>
    public const string ErrorReportingUnavailable = "ErrorReportingUnavailable";

    /// <summary>
    /// The connected client returned an unsupported approval response.
    /// </summary>
    public const string InvalidApprovalResponse = "InvalidApprovalResponse";

    /// <summary>
    /// The selected exception-message handling mode is invalid.
    /// </summary>
    public const string InvalidExceptionMessageHandling = "InvalidExceptionMessageHandling";

    /// <summary>
    /// The immutable error report does not match its prepared submission.
    /// </summary>
    public const string InvalidPreparedErrorReport = "InvalidPreparedErrorReport";

    /// <summary>
    /// The MCP request could not be bound to its published request contract.
    /// </summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>
    /// The prepared report handle is unknown or expired.
    /// </summary>
    public const string PreparedReportUnavailable = "PreparedReportUnavailable";

    /// <summary>
    /// Recovery evidence identifies a Workspace outside current Host authority.
    /// </summary>
    public const string RecoveryOutsideWorkspaceAuthority = "RecoveryOutsideWorkspaceAuthority";

    /// <summary>
    /// The Sentry SDK declined the prepared error report.
    /// </summary>
    public const string SentryCaptureRejected = "SentryCaptureRejected";

    /// <summary>
    /// Transaction commit did not receive the required user confirmation.
    /// </summary>
    public const string TransactionCommitNotApproved = "TransactionCommitNotApproved";

    /// <summary>
    /// The supplied transaction receipt is unavailable or expired.
    /// </summary>
    public const string TransactionReceiptUnavailable = "TransactionReceiptUnavailable";

    /// <summary>
    /// Tool execution failed with an unexpected exception.
    /// </summary>
    public const string UnhandledException = "UnhandledException";
}
