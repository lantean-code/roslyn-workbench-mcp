namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests removal of unused using directives across a selected scope.
/// </summary>
internal sealed record RemoveUnusedUsingsRequest : WorkspaceBoundRequest
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
