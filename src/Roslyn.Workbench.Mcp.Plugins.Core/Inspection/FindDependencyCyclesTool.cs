using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

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
        if (!context.ToolExecutionServices.DependencyAnalysisService.IsSupportedCycleGranularity(request.Granularity))
        {
            return ToolExecutionHelpers.Rejected<DependencyCyclesData>("InvalidRequest", "Granularity must be Project, Namespace, or Type.");
        }

        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<DependencyCyclesData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var projects = context.ToolExecutionServices.RequestResolver.ResolveProjects<DependencyCyclesData>(request.Scope, context);
        if (projects.HasRejection)
        {
            return projects.Rejection;
        }

        var cycles = await context.ToolExecutionServices.DependencyAnalysisService.FindCyclesAsync(
            request.Granularity,
            projects.Value,
            documents.Value,
            context,
            cancellationToken).ConfigureAwait(false);

        return PluginExecutionResult<DependencyCyclesData>.Success(new DependencyCyclesData
        {
            Cycles = ToolExecutionHelpers.CreateBoundedCollection(
                cycles,
                ToolExecutionHelpers.GetMaxResults(context, request.CyclesLimit)),
        });
    }
}
