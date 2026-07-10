using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

internal interface ISnapshotGuard
{
    WorkspaceOperationError? Validate(WorkspaceSessionSnapshot session, SnapshotPrecondition? expectedSnapshot);
}
