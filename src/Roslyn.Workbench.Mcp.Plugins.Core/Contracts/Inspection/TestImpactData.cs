namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-test-impact.
/// </summary>
internal sealed record TestImpactData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned impacted tests.
    /// </summary>
    [Description("The returned impacted tests.")]
    public BoundedCollection<TestImpactInfo> Tests { get; init; } = BoundedCollection.Empty<TestImpactInfo>();
}
