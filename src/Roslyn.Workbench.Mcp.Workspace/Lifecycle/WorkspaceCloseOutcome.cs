namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

/// <summary>
/// Describes the workspace released by a successful close operation.
/// </summary>
internal sealed record WorkspaceCloseOutcome
{
    /// <summary>
    /// Gets the solution or project path that was closed.
    /// </summary>
    public required string ClosedPath { get; init; }
}
