using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests removal of unused using directives across a selected scope.
/// </summary>
public sealed record RemoveUnusedUsingsRequest
{
    /// <summary>
    /// Gets the scope to clean.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the staged mutation.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
