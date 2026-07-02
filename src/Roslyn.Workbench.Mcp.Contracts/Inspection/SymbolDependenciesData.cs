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
    public IReadOnlyList<DependencyInfo> Dependencies { get; init; } = [];

    /// <summary>
    /// Gets the number of dependencies returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more dependencies were available.
    /// </summary>
    public bool HasMore { get; init; }
}
