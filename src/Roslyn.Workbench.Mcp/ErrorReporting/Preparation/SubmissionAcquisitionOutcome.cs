namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

/// <summary>
/// Identifies the result of acquiring a prepared submission for single dispatch.
/// </summary>
internal enum SubmissionAcquisitionOutcome
{
    /// <summary>
    /// The prepared submission was moved to the sending state for this caller.
    /// </summary>
    Acquired,
    /// <summary>
    /// The handle is unknown or its prepared submission has expired.
    /// </summary>
    UnknownOrExpired,
    /// <summary>
    /// Another caller has already acquired the submission for dispatch.
    /// </summary>
    InProgress,
    /// <summary>
    /// The submission has already been accepted by the provider.
    /// </summary>
    AlreadySent,
}
