namespace Roslyn.Workbench.Mcp.Workspace.Hierarchy;

/// <summary>
/// Represents one type discovered below a hierarchy root.
/// </summary>
public sealed record TypeHierarchyMatch
{
    /// <summary>
    /// Gets the discovered type.
    /// </summary>
    public required INamedTypeSymbol Type { get; init; }

    /// <summary>
    /// Gets the shortest inheritance distance from the hierarchy root.
    /// </summary>
    public required int Depth { get; init; }
}
