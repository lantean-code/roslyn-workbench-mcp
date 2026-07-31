namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

internal sealed record ExternalWorkspaceContext
{
    public required string LifecycleState { get; init; }

    public long WorkspaceEpoch { get; init; }

    public int ProjectCount { get; init; }

    public int DocumentCount { get; init; }

    public int? TransactionRevision { get; init; }
}
