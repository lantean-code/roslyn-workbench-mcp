namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one future Roslyn-backed property conversion at a selected property declaration.
/// </summary>
internal sealed record ConvertPropertyRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the selected property declaration to rewrite.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the property-conversion direction to stage.
    /// </summary>
    public ConvertPropertyDirection Direction { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
