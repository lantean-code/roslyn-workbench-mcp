namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal sealed record ErrorDispatchResult
{
    public required ErrorDispatchOutcome Outcome { get; init; }

    public string? ReportReference { get; init; }

    public string? PayloadDigest { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}
