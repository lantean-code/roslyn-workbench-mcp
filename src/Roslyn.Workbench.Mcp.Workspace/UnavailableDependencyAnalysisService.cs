using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class UnavailableDependencyAnalysisService : IDependencyAnalysisService
{
    private const string _message = "Tool execution services are unavailable.";

    public bool IsSupportedCycleGranularity(string value)
    {
        _ = value;

        return false;
    }

    public bool IsSupportedGraphGranularity(string value)
    {
        _ = value;

        return false;
    }

    public ValueTask<IReadOnlyList<DependencyCycle>> FindCyclesAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        _ = granularity;
        _ = projects;
        _ = documents;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromException<IReadOnlyList<DependencyCycle>>(new InvalidOperationException(_message));
    }

    public ValueTask<IReadOnlyList<TestImpactInfo>> FindTestImpactsAsync(
        ISymbol targetSymbol,
        IReadOnlyList<Document> documents,
        bool includeReasons,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        _ = targetSymbol;
        _ = documents;
        _ = includeReasons;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromException<IReadOnlyList<TestImpactInfo>>(new InvalidOperationException(_message));
    }

    public ValueTask<(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)> BuildGraphAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        _ = granularity;
        _ = projects;
        _ = documents;
        _ = context;
        _ = cancellationToken;

        return ValueTask.FromException<(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)>(new InvalidOperationException(_message));
    }
}
