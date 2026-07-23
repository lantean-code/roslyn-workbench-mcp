namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests conversion of a supported string expression to an interpolated string through Roslyn refactoring composition.
/// </summary>
internal sealed record ConvertToInterpolatedStringRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected string expression to convert.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
