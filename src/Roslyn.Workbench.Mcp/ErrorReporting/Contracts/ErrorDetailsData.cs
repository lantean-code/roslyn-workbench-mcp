namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record ErrorDetailsData
{
    public string Sensitivity { get; init; } = "LocalDiagnostic";

    public bool SafeForExternalSubmission { get; init; }

    public required CapturedErrorRecord Error { get; init; }
}
