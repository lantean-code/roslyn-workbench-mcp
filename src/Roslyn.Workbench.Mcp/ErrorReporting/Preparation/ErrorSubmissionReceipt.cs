namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

internal sealed record ErrorSubmissionReceipt
{
    public required string Dispatcher { get; init; }

    public required string ReportReference { get; init; }

    public required string PayloadDigest { get; init; }
}
