using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Performs Roslyn-based dependency analysis used by inspection tools.
/// </summary>
public interface IDependencyAnalysisService
{
    /// <summary>
    /// Determines whether a dependency-cycle granularity value is supported.
    /// </summary>
    /// <param name="value">The granularity value.</param>
    /// <returns><see langword="true" /> when the value is supported; otherwise <see langword="false" />.</returns>
    bool IsSupportedCycleGranularity(string value);

    /// <summary>
    /// Determines whether a dependency-graph granularity value is supported.
    /// </summary>
    /// <param name="value">The granularity value.</param>
    /// <returns><see langword="true" /> when the value is supported; otherwise <see langword="false" />.</returns>
    bool IsSupportedGraphGranularity(string value);

    /// <summary>
    /// Finds dependency cycles for the provided scope and granularity.
    /// </summary>
    /// <param name="granularity">The graph granularity.</param>
    /// <param name="projects">The resolved projects.</param>
    /// <param name="documents">The resolved documents.</param>
    /// <param name="context">The current query context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The detected dependency cycles.</returns>
    ValueTask<IReadOnlyList<DependencyCycle>> FindCyclesAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds likely impacted tests for the supplied symbol.
    /// </summary>
    /// <param name="targetSymbol">The target symbol.</param>
    /// <param name="documents">The candidate test documents.</param>
    /// <param name="includeReasons">Whether explanatory reasons should be included.</param>
    /// <param name="context">The current query context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The impacted tests.</returns>
    ValueTask<IReadOnlyList<TestImpactInfo>> FindTestImpactsAsync(
        ISymbol targetSymbol,
        IReadOnlyList<Document> documents,
        bool includeReasons,
        IQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds a dependency graph for the provided scope and granularity.
    /// </summary>
    /// <param name="granularity">The graph granularity.</param>
    /// <param name="projects">The resolved projects.</param>
    /// <param name="documents">The resolved documents.</param>
    /// <param name="context">The current query context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The graph nodes and edges.</returns>
    ValueTask<(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)> BuildGraphAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken);
}
