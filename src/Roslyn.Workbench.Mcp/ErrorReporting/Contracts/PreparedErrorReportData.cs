namespace Roslyn.Workbench.Mcp.ErrorReporting.Contracts;

internal sealed record PreparedErrorReportData
{
    public required string SubmissionHandle { get; init; }

    public required string Dispatcher { get; init; }

    public required string Destination { get; init; }

    public required string PayloadDigest { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required string PayloadJson { get; init; }

    public IReadOnlyList<string> ExcludedCategories { get; init; } = [];

    public IReadOnlyList<string> ReviewWarnings { get; init; } = [];
}
