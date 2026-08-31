namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Controls whether captured exception messages are retained in the submitted payload.
/// </summary>
internal enum ExceptionMessageHandling
{
    /// <summary>
    /// Submit the exception messages presented for review.
    /// </summary>
    Include,
    /// <summary>
    /// Redact exception messages immediately before dispatch.
    /// </summary>
    Remove,
}
