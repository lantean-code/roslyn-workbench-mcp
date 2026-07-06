namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceSelection
{
    public required string WorkspaceId { get; init; }

    public required WorkspaceSessionSnapshot Session { get; init; }
}
