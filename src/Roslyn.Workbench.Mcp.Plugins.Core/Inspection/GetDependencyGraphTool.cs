namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-dependency-graph", "Get Dependency Graph", "Returns a bounded dependency graph for the selected scope and granularity.")]
internal sealed class GetDependencyGraphTool : QueryToolHandler<GetDependencyGraphRequest, DependencyGraphData>
{
    protected override async ValueTask<PluginExecutionResult<DependencyGraphData>> ExecuteCoreAsync(GetDependencyGraphRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (!context.ToolExecutionServices.DependencyAnalysisService.IsSupportedGraphGranularity(request.Granularity))
        {
            return PluginExecutionResultFactory.Rejected<DependencyGraphData>("InvalidRequest", "Granularity must be Project, Namespace, Type, or Symbol.");
        }

        if (request.NodesLimit is < 0 || request.EdgesLimit is < 0)
        {
            return PluginExecutionResultFactory.Rejected<DependencyGraphData>("InvalidRequest", "NodesLimit and EdgesLimit must be zero or greater when provided.");
        }

        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<DependencyGraphData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var projects = context.ToolExecutionServices.RequestResolver.ResolveProjects<DependencyGraphData>(request.Scope, context);
        if (projects.HasRejection)
        {
            return projects.Rejection;
        }

        var (nodes, nodesHaveMore, edges, edgesHaveMore) = await context.ToolExecutionServices.DependencyAnalysisService.BuildGraphAsync(
            request.Granularity,
            projects.Value,
            documents.Value,
            request.EffectiveNodesLimit,
            request.EffectiveEdgesLimit,
            context,
            cancellationToken);

        var data = new DependencyGraphData
        {
            Nodes = BoundedCollection<GraphNode>.CreatePrebounded(nodes, nodesHaveMore),
            Edges = BoundedCollection<GraphEdge>.CreatePrebounded(edges, edgesHaveMore),
        };

        return PluginExecutionResult<DependencyGraphData>.Success(data);
    }
}
