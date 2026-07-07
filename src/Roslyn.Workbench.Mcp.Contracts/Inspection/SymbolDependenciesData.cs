using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-dependencies.
/// </summary>
public sealed record SymbolDependenciesData
{
    /// <summary>
    /// Gets the queried symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the returned direct dependencies.
    /// </summary>
    public BoundedCollection<DependencyInfo> Dependencies { get; init; } = BoundedCollection<DependencyInfo>.Empty();
}
