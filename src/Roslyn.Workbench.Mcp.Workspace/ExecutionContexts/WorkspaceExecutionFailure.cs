namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

internal sealed record WorkspaceExecutionFailure
{
    public WorkspaceOperationStatus Status { get; init; }

    public WorkspaceOperationError Error { get; init; } = new();
}
