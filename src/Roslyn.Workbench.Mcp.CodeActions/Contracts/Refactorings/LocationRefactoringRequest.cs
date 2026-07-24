namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests a selection-based Roslyn refactoring through refactoring composition.
/// </summary>
internal sealed record LocationRefactoringRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected location that identifies the Roslyn refactoring target.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
