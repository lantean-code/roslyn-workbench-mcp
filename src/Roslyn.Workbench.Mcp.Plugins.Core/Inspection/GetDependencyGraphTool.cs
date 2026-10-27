namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns a bounded dependency graph for the selected scope and granularity.
/// </summary>
[RoslynTool("get-dependency-graph", "Get Dependency Graph", "Builds a dependency graph at Project, Namespace, Type or Symbol granularity, with edges directed from dependents to dependencies. Use this to inspect relationships across a scope; use get-symbol-dependencies or get-symbol-dependents for one symbol, or get-change-impact for a symbol-centred count summary. Project granularity uses selected projects; other granularities use selected source documents. Omitted scope covers the loaded solution. Defaults to Type granularity, 200 nodes and 400 edges. Check both nodes.hasMore and edges.hasMore; omitted nodes also exclude their connected edges.")]
internal sealed class GetDependencyGraphTool : QueryToolHandler<GetDependencyGraphRequest, DependencyGraphData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<DependencyGraphData>> ExecuteCoreAsync(GetDependencyGraphRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (!context.ToolExecutionServices.DependencyAnalysisService.IsSupportedGraphGranularity(request.Granularity))
        {
            return PluginExecutionResult.Rejected<DependencyGraphData>(PluginErrorCodes.InvalidRequest, "Granularity must be Project, Namespace, Type, or Symbol.");
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
            Nodes = BoundedCollection.CreatePrebounded(nodes, nodesHaveMore),
            Edges = BoundedCollection.CreatePrebounded(edges, edgesHaveMore),
        };

        return PluginExecutionResult.Success(data);
    }
}
