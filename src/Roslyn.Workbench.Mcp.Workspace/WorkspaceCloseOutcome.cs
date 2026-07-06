namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceCloseOutcome
{
    public string ClosedPath { get; init; } = string.Empty;
}
