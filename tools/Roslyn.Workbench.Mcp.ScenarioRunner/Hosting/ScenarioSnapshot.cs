namespace Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;

internal sealed record ScenarioSnapshot
{
    public required Guid WorkspaceId { get; init; }

    public required long WorkspaceEpoch { get; init; }

    public required Guid SnapshotId { get; init; }

    public required int? TransactionRevision { get; init; }

}
