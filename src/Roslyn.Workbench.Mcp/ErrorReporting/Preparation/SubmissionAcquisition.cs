namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

/// <summary>
/// Reports whether a prepared submission was acquired for dispatch and returns its current state when available.
/// </summary>
internal sealed record SubmissionAcquisition
{
    /// <summary>
    /// Gets the result of the atomic acquisition attempt.
    /// </summary>
    public required SubmissionAcquisitionOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the acquired or previously processed submission when the handle was found.
    /// </summary>
    public PreparedSubmission? Submission { get; init; }
}
