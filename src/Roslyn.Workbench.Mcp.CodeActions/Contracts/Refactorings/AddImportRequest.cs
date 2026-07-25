namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported add-import refactoring through Roslyn refactoring composition.
/// </summary>
internal sealed record AddImportRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected qualified type reference to rewrite.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets a value indicating whether all matching occurrences should also be simplified.
    /// </summary>
    public bool SimplifyAllOccurrences { get; init; }
}
