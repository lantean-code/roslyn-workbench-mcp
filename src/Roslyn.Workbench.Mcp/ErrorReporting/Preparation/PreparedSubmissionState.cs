namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

/// <summary>
/// Tracks a prepared report through its single-dispatch lifecycle.
/// </summary>
internal enum PreparedSubmissionState
{
    /// <summary>
    /// The payload is available for review or dispatch.
    /// </summary>
    Prepared,
    /// <summary>
    /// One caller has acquired the payload for dispatch.
    /// </summary>
    Sending,
    /// <summary>
    /// The provider accepted the payload and a receipt was recorded.
    /// </summary>
    Sent,
}
