namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-dependency-cycles", "Find Dependency Cycles", "Returns detected dependency cycles for the selected scope and granularity.")]
internal sealed class FindDependencyCyclesTool : QueryToolHandler<FindDependencyCyclesRequest, DependencyCyclesData>
{
    protected override async ValueTask<PluginExecutionResult<DependencyCyclesData>> ExecuteCoreAsync(FindDependencyCyclesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (!context.ToolExecutionServices.DependencyAnalysisService.IsSupportedCycleGranularity(request.Granularity))
        {
            return PluginExecutionResult.Rejected<DependencyCyclesData>("InvalidRequest", "Granularity must be Project, Namespace, or Type.");
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

        var (cycles, totalCount) = await context.ToolExecutionServices.DependencyAnalysisService.FindCyclesAsync(
            request.Granularity,
            projects.Value,
            documents.Value,
            request.EffectiveCyclesLimit,
            context,
            cancellationToken);

        var data = new DependencyCyclesData
        {
            Cycles = BoundedCollection.CreatePrebounded(cycles, totalCount),
        };

        return PluginExecutionResult.Success(data);
    }
}
