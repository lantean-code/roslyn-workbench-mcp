using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests conversion of a supported if-chain to a switch form through Roslyn refactoring composition.
/// </summary>
public sealed record ConvertIfToSwitchRequest
{
    /// <summary>
    /// Gets the selected if-chain to convert.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the target switch form to stage.
    /// </summary>
    public ConvertIfToSwitchKind Kind { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
