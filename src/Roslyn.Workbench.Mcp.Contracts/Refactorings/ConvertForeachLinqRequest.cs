using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests one supported Roslyn foreach or LINQ conversion through refactoring composition.
/// </summary>
public sealed record ConvertForeachLinqRequest
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
