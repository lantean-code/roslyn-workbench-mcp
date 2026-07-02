using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests conversion of a supported auto-property to a full property through Roslyn refactoring composition.
/// </summary>
public sealed record ConvertAutoPropertyToFullPropertyRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected auto-property declaration to rewrite.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
