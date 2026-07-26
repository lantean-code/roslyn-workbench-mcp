namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-change-impact.
/// </summary>
internal sealed record ChangeImpactData
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
    public BoundedCollection<ReferenceLocation> Locations { get; init; } = BoundedCollection.Empty<ReferenceLocation>();
}
