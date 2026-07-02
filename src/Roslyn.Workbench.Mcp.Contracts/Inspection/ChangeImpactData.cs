using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-change-impact.
/// </summary>
public sealed record ChangeImpactData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the bounded impact summary.
    /// </summary>
    public ImpactSummary? Impact { get; init; }

    /// <summary>
    /// Gets the returned supporting source locations.
    /// </summary>
    public IReadOnlyList<ReferenceLocation> Locations { get; init; } = [];

    /// <summary>
    /// Gets the number of locations returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more locations were available.
    /// </summary>
    public bool HasMore { get; init; }
}
