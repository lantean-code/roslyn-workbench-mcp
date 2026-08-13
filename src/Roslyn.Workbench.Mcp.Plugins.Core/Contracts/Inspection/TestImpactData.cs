namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-test-impact.
/// </summary>
internal sealed record TestImpactData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned impacted tests.
    /// </summary>
    public BoundedCollection<TestImpactInfo> Tests { get; init; } = BoundedCollection.Empty<TestImpactInfo>();
}
