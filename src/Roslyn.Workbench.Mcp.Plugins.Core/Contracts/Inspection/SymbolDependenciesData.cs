namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-dependencies.
/// </summary>
internal sealed record SymbolDependenciesData : IQueryResponse
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    [Description("The queried symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned direct dependencies.
    /// </summary>
    [Description("The returned direct dependencies.")]
    public BoundedCollection<DependencyInfo> Dependencies { get; init; } = BoundedCollection.Empty<DependencyInfo>();
}
