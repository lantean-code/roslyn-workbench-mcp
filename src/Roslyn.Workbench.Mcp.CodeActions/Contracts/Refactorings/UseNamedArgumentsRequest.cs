using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported use-named-arguments refactoring through Roslyn refactoring composition.
/// </summary>
public sealed record UseNamedArgumentsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected argument location.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets a value indicating whether trailing arguments should also receive names.
    /// </summary>
    public bool IncludeTrailingArguments { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
