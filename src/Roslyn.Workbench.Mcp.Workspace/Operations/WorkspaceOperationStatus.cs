namespace Roslyn.Workbench.Mcp.Workspace.Operations;

internal enum WorkspaceOperationStatus
{
    Succeeded,
    Rejected,
    Conflict,
    Faulted,
    NoChange,
}
