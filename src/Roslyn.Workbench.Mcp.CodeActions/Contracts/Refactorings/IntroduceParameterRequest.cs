namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests promotion of a selected expression to a method parameter through Roslyn refactoring composition.
/// </summary>
internal sealed record IntroduceParameterRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected expression to promote.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets a value indicating whether all matching occurrences should be promoted.
    /// </summary>
    public bool AllOccurrences { get; init; }

    /// <summary>
    /// Gets the introduce-parameter strategy to stage.
    /// </summary>
    public IntroduceParameterStrategy Strategy { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
