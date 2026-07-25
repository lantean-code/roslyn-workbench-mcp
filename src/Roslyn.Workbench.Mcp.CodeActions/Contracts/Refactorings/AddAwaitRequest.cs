namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported add-await refactoring through Roslyn refactoring composition.
/// </summary>
internal sealed record AddAwaitRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected async expression to rewrite.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets the add-await variant to stage.
    /// </summary>
    public required AddAwaitKind Kind { get; init; }
}
