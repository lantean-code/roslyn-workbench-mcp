namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceSelectionResult
{
    public WorkspaceSelection? Selection { get; init; }

    public WorkspaceOperationError? Error { get; init; }
}
