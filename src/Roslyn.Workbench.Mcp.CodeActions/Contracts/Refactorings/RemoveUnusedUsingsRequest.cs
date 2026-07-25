namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests removal of unused using directives across a selected scope.
/// </summary>
internal sealed record RemoveUnusedUsingsRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the scope to clean.
    /// </summary>
    public required ScopeSelector Scope { get; init; }
}
