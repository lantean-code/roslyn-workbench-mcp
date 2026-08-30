namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal sealed record WorkspaceCloseOutcome
{
    public required string ClosedPath { get; init; }
}
