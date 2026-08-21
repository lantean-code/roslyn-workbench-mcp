namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface ISnapshotGuard
{
    SnapshotValidationResult Validate(WorkspaceSessionSnapshot session, SnapshotPrecondition? expectedSnapshot);
}
