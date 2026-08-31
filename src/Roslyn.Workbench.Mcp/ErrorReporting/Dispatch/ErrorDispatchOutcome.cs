namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Indicates whether an error-reporting provider accepted or rejected a submission.
/// </summary>
internal enum ErrorDispatchOutcome
{
    /// <summary>
    /// The provider accepted the report.
    /// </summary>
    Accepted,
    /// <summary>
    /// The provider rejected the report before or during dispatch.
    /// </summary>
    Rejected,
}
