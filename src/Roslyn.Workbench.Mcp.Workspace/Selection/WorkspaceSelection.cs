namespace Roslyn.Workbench.Mcp.Workspace.Selection;

/// <summary>
/// Couples a selected workspace identifier with its live session.
/// </summary>
internal sealed record WorkspaceSelection
{
    /// <summary>
    /// Gets the selected workspace identifier.
    /// </summary>
    public required Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the selected live workspace session.
    /// </summary>
    public required WorkspaceSessionSnapshot Session { get; init; }
}
