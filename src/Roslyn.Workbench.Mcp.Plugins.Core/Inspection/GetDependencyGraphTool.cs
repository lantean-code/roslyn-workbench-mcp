using Roslyn.Workbench.Mcp.Contracts.Inspection;
namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GetDependencyGraphTool : QueryToolHandler<GetDependencyGraphRequest, DependencyGraphData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-dependency-graph",
        Title = "Get Dependency Graph",
        Description = "Returns a bounded dependency graph for the selected scope and granularity.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetDependencyGraphTool());
    }

    protected override async ValueTask<PluginExecutionResult<DependencyGraphData>> ExecuteCoreAsync(GetDependencyGraphRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.ToolExecutionServices.DependencyAnalysisService.IsSupportedGraphGranularity(request.Granularity))
        {
            return ToolExecutionHelpers.Rejected<DependencyGraphData>("InvalidRequest", "Granularity must be Project, Namespace, Type, or Symbol.");
        }

        if (request.MaxDepth < 0)
        {
            return ToolExecutionHelpers.Rejected<DependencyGraphData>("InvalidRequest", "MaxDepth must be zero or greater.");
        }

        if (request.NodesLimit?.MaxResults is < 0 || request.EdgesLimit?.MaxResults is < 0)
        {
            return ToolExecutionHelpers.Rejected<DependencyGraphData>("InvalidRequest", "NodesLimit and EdgesLimit must be zero or greater when provided.");
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

        var graph = await context.ToolExecutionServices.DependencyAnalysisService.BuildGraphAsync(
            request.Granularity,
            projects.Value,
            documents.Value,
            context,
            cancellationToken).ConfigureAwait(false);
        var nodes = ToolExecutionHelpers.CreateBoundedCollection(
            graph.Nodes,
            ToolExecutionHelpers.GetMaxResults(context, request.NodesLimit));
        var nodeIds = nodes.Items.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges
            .Where(edge => nodeIds.Contains(edge.FromId) && nodeIds.Contains(edge.ToId))
            .ToArray();
        return PluginExecutionResult<DependencyGraphData>.Success(new DependencyGraphData
        {
            Nodes = nodes,
            Edges = ToolExecutionHelpers.CreateBoundedCollection(
                edges,
                ToolExecutionHelpers.GetMaxResults(context, request.EdgesLimit)),
        });
    }
}
