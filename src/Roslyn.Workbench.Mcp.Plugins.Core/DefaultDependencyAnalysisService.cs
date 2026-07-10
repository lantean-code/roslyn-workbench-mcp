using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class DefaultDependencyAnalysisService : IDependencyAnalysisService
{
    public bool IsSupportedCycleGranularity(string value)
    {
        return DependencyAnalysisHelpers.IsSupportedCycleGranularity(value);
    }

    public bool IsSupportedGraphGranularity(string value)
    {
        return DependencyAnalysisHelpers.IsSupportedGraphGranularity(value);
    }

    public ValueTask<IReadOnlyList<DependencyCycle>> FindCyclesAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        return DependencyAnalysisHelpers.FindCyclesAsync(granularity, projects, documents, context, cancellationToken);
    }

    public ValueTask<IReadOnlyList<TestImpactInfo>> FindTestImpactsAsync(
        ISymbol targetSymbol,
        IReadOnlyList<Document> documents,
        bool includeReasons,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        return DependencyAnalysisHelpers.FindTestImpactsAsync(targetSymbol, documents, includeReasons, context, cancellationToken);
    }

    public ValueTask<(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)> BuildGraphAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        return DependencyAnalysisHelpers.BuildGraphAsync(granularity, projects, documents, context, cancellationToken);
    }
}
