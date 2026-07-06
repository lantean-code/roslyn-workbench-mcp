using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Results;

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

        if (request.MaxNodes is <= 0 || request.MaxEdges is <= 0)
        {
            return ToolExecutionHelpers.Rejected<DependencyGraphData>("InvalidRequest", "MaxNodes and MaxEdges must be greater than zero when provided.");
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
        var truncationReasons = new List<CollectionTruncation>();
        var nodes = graph.Nodes;

        if (request.MaxNodes is { } maxNodes && nodes.Count > maxNodes)
        {
            nodes = nodes.Take(maxNodes).ToArray();
            truncationReasons.Add(CollectionTruncation.NodeLimit);
        }

        var nodeIds = nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges
            .Where(edge => nodeIds.Contains(edge.FromId) && nodeIds.Contains(edge.ToId))
            .ToArray();

        if (request.MaxEdges is { } maxEdges && edges.Length > maxEdges)
        {
            edges = edges.Take(maxEdges).ToArray();
            truncationReasons.Add(CollectionTruncation.EdgeLimit);
        }

        return context.ToolExecutionServices.ResultShaper.EnsureWithinSize(context, new DependencyGraphData
        {
            Nodes = nodes,
            Edges = edges,
            ReturnedNodeCount = nodes.Count,
            ReturnedEdgeCount = edges.Length,
            TruncationReasons = truncationReasons,
        });
    }
}
