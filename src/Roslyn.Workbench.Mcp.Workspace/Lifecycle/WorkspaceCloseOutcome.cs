namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal sealed record WorkspaceCloseOutcome
{
    public string ClosedPath { get; init; } = string.Empty;
}
