namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-change-impact.
/// </summary>
internal sealed record ChangeImpactData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the bounded impact summary.
    /// </summary>
    [Description("The bounded impact summary.")]
    public ImpactSummary? Impact { get; init; }

    /// <summary>
    /// Gets the returned supporting source locations.
    /// </summary>
    [Description("The returned supporting source locations.")]
    public BoundedCollection<ReferenceLocation> Locations { get; init; } = BoundedCollection.Empty<ReferenceLocation>();
}
