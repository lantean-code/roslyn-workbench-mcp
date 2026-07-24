namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests extraction of a selected statement or expression block through Roslyn refactoring composition.
/// </summary>
internal sealed record ExtractMethodRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected code to extract.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets the extract-method variant to stage.
    /// </summary>
    public ExtractMethodTargetKind TargetKind { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
