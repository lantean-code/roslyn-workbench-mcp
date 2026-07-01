namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceSelection
{
    public string WorkspaceId { get; init; } = string.Empty;

    public WorkspaceSessionSnapshot Session { get; init; } = new();
}
