using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class FindDependencyCyclesTool : QueryToolHandler<FindDependencyCyclesRequest, DependencyCyclesData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-dependency-cycles",
        Title = "Find Dependency Cycles",
        Description = "Returns detected dependency cycles for the selected scope and granularity.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindDependencyCyclesTool());
    }

    protected override async ValueTask<PluginExecutionResult<DependencyCyclesData>> ExecuteCoreAsync(FindDependencyCyclesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!DependencyAnalysisHelpers.IsSupportedCycleGranularity(request.Granularity))
        {
            return ToolExecutionHelpers.Rejected<DependencyCyclesData>("InvalidRequest", "Granularity must be Project, Namespace, or Type.");
        }

        var documents = ToolExecutionHelpers.ResolveDocuments<DependencyCyclesData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var projects = ToolExecutionHelpers.ResolveProjects<DependencyCyclesData>(request.Scope, context);
        if (projects.HasRejection)
        {
            return projects.Rejection;
        }

        var cycles = await DependencyAnalysisHelpers.FindCyclesAsync(
            request.Granularity,
            projects.Value,
            documents.Value,
            context,
            cancellationToken).ConfigureAwait(false);

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            cycles,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            (items, hasMore) => new DependencyCyclesData
            {
                Cycles = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }
}
