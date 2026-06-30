using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests extraction of a selected statement or expression block through Roslyn refactoring composition.
/// </summary>
public sealed record ExtractMethodRequest
{
    /// <summary>
    /// Gets the selected code to extract.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the extract-method variant to stage.
    /// </summary>
    public ExtractMethodTargetKind TargetKind { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
