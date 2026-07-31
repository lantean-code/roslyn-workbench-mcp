namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record SubmittedErrorReportData
{
    public required string Dispatcher { get; init; }

    public required string ReportReference { get; init; }

    public required string PayloadDigest { get; init; }
}
