namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record ErrorDetailsData
{
    /// <summary>
    /// Gets the Sensitivity.
    /// </summary>
    [Description("Sensitivity classification applied to the captured error details.")]
    public string Sensitivity { get; init; } = "LocalDiagnostic";

    /// <summary>
    /// Gets the Safe For External Submission.
    /// </summary>
    [Description("Whether the captured details are safe to submit outside the local process.")]
    public bool SafeForExternalSubmission { get; init; }

    /// <summary>
    /// Gets the Error.
    /// </summary>
    [Description("Captured diagnostic record for the failed tool invocation.")]
    public required CapturedErrorRecord Error { get; init; }
}
