namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported Roslyn foreach or LINQ conversion through refactoring composition.
/// </summary>
internal sealed record ConvertForeachLinqRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected foreach statement or query expression to convert.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the conversion variant to stage.
    /// </summary>
    public ConvertForeachLinqKind ConversionKind { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
