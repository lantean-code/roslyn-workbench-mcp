using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests one supported anonymous-type-to-named-type refactoring through Roslyn refactoring composition.
/// </summary>
public sealed record ConvertAnonymousTypeToClassRequest
{
    /// <summary>
    /// Gets the selected anonymous object creation to rewrite.
    /// </summary>
    public LocationSelector? Selection { get; init; }

    /// <summary>
    /// Gets the named-type variant to stage.
    /// </summary>
    public ConvertAnonymousTypeToClassKind Kind { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected location.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
