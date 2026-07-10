using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported add-await refactoring through Roslyn refactoring composition.
/// </summary>
public sealed record AddAwaitRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected async expression to rewrite.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the add-await variant to stage.
    /// </summary>
    public AddAwaitKind Kind { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
