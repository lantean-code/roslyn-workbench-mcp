namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

internal sealed record SubmissionAcquisition
{
    public required SubmissionAcquisitionOutcome Outcome { get; init; }

    public PreparedSubmission? Submission { get; init; }
}
