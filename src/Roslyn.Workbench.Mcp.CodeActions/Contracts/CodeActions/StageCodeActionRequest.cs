using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents a request to stage a selected refactoring action.
/// </summary>
public sealed record StageCodeActionRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the opaque action token.
    /// </summary>
    public string ActionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expected workspace snapshot.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
