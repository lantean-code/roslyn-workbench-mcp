using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests a selection-based Roslyn refactoring through refactoring composition.
/// </summary>
public sealed record LocationRefactoringRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected location that identifies the Roslyn refactoring target.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
