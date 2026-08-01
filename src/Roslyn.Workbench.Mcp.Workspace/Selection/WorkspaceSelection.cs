namespace Roslyn.Workbench.Mcp.Workspace.Selection;

internal sealed record WorkspaceSelection
{
    public required Guid WorkspaceId { get; init; }

    public required WorkspaceSessionSnapshot Session { get; init; }
}
