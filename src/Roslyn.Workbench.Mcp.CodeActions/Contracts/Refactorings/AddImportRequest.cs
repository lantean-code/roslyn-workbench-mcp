using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported add-import refactoring through Roslyn refactoring composition.
/// </summary>
public sealed record AddImportRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected qualified type reference to rewrite.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets a value indicating whether all matching occurrences should also be simplified.
    /// </summary>
    public bool SimplifyAllOccurrences { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
