namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one projected metric row.
/// </summary>
internal sealed record MetricInfo
{
    /// <summary>
    /// Gets the associated symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the source location for the metric row.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the logical line count.
    /// </summary>
    public int LogicalLines { get; init; }

    /// <summary>
    /// Gets the cyclomatic complexity.
    /// </summary>
    public int CyclomaticComplexity { get; init; }

    /// <summary>
    /// Gets the maximum nesting depth.
    /// </summary>
    public int MaxNestingDepth { get; init; }

    /// <summary>
    /// Gets the distinct type-coupling count.
    /// </summary>
    public int Coupling { get; init; }

    /// <summary>
    /// Gets the derived maintainability score.
    /// </summary>
    public int MaintainabilityIndex { get; init; }
}
