namespace Roslyn.Workbench.Mcp.Workspace;

internal enum WorkspaceOperationStatus
{
    Succeeded,
    Rejected,
    Conflict,
    Faulted,
    NoChange,
}
