namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

/// <summary>
/// Returns the locally retained diagnostic details for a failed tool invocation.
/// </summary>
internal sealed record ErrorDetailsData
{
    /// <summary>
    /// Sensitivity classification applied to the captured error details.
    /// </summary>
    [Description("Sensitivity classification applied to the captured error details.")]
    public string Sensitivity { get; init; } = "LocalDiagnostic";

    /// <summary>
    /// Whether the captured details are safe to submit outside the local process.
    /// </summary>
    [Description("Whether the captured details are safe to submit outside the local process.")]
    public bool SafeForExternalSubmission { get; init; }

    /// <summary>
    /// Captured diagnostic record for the failed tool invocation.
    /// </summary>
    [Description("Captured diagnostic record for the failed tool invocation.")]
    public required CapturedErrorRecord Error { get; init; }
}
