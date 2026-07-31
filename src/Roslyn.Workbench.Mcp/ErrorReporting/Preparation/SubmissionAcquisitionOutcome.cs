namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

internal enum SubmissionAcquisitionOutcome
{
    Acquired,
    UnknownOrExpired,
    InProgress,
    AlreadySent,
}
