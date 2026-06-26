using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.CodeActions;

/// <summary>
/// Represents a request to stage a selected refactoring action.
/// </summary>
public sealed record StageCodeActionRequest
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
