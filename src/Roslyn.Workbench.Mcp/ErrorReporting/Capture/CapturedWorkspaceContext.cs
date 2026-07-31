namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed record CapturedWorkspaceContext
{
    public required string WorkspaceId { get; init; }

    public required long WorkspaceEpoch { get; init; }

    public required string LifecycleState { get; init; }

    public int ProjectCount { get; init; }

    public int DocumentCount { get; init; }

    public int? TransactionRevision { get; init; }
}
