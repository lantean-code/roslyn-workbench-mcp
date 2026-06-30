using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests one supported use-named-arguments refactoring through Roslyn refactoring composition.
/// </summary>
public sealed record UseNamedArgumentsRequest
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
