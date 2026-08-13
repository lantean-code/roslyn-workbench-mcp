namespace Roslyn.Workbench.Mcp.Plugins.Services;

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
    /// <param name="maxResults">The maximum number of cycles to return.</param>
    /// <param name="maxNodes">The maximum number of graph nodes to analyse.</param>
    /// <param name="maxEdges">The maximum number of graph edges to analyse.</param>
    /// <param name="context">The current query context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete analysis outcome.</returns>
    ValueTask<DependencyCycleAnalysisResult> FindCyclesAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        int maxResults,
        int maxNodes,
        int maxEdges,
        IQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds likely impacted tests for the supplied symbol.
    /// </summary>
    /// <param name="targetSymbol">The target symbol.</param>
    /// <param name="documents">The candidate test documents.</param>
    /// <param name="includeReasons">Whether explanatory reasons should be included.</param>
    /// <param name="maxResults">The maximum number of impacted tests to return.</param>
    /// <param name="context">The current query context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The impacted tests and whether additional tests were found.</returns>
    ValueTask<(IReadOnlyList<TestImpactInfo> Tests, bool HasMore)> FindTestImpactsAsync(
        ISymbol targetSymbol,
        IReadOnlyList<Document> documents,
        bool includeReasons,
        int maxResults,
        IQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Builds a dependency graph for the provided scope and granularity.
    /// </summary>
    /// <param name="granularity">The graph granularity.</param>
    /// <param name="projects">The resolved projects.</param>
    /// <param name="documents">The resolved documents.</param>
    /// <param name="maxNodes">The maximum number of nodes to return.</param>
    /// <param name="maxEdges">The maximum number of edges to return.</param>
    /// <param name="context">The current query context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The graph nodes and edges together with their truncation states.</returns>
    ValueTask<(IReadOnlyList<GraphNode> Nodes, bool NodesHaveMore, IReadOnlyList<GraphEdge> Edges, bool EdgesHaveMore)> BuildGraphAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        int maxNodes,
        int maxEdges,
        IQueryContext context,
        CancellationToken cancellationToken);
}
