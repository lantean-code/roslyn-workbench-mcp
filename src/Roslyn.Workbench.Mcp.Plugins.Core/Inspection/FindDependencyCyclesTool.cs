namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns detected dependency cycles for the selected scope and granularity.
/// </summary>
[RoslynTool("find-dependency-cycles", "Find Dependency Cycles", "Finds mutually dependent groups in a static dependency graph for the selected scope, or the loaded solution when scope is omitted. Use this to investigate circular coupling; use get-dependency-graph to inspect individual dependency edges. granularity defaults to Type; Namespace groups source dependencies and Project uses direct project references. A document scope uses its containing project for Project granularity. Returns a bounded cycles collection of strongly connected node groups, not ordered traversal paths. cyclesLimit defaults to 25; check cycles.hasMore for additional groups. Analysis budgets default to 25000 nodes and 100000 edges; exceeding either rejects the query with AnalysisLimitExceeded rather than returning a partial cycle analysis. Narrow the scope or raise the relevant budget to retry. Read-only; no tests or application code are run.")]
internal sealed class FindDependencyCyclesTool : QueryToolHandler<FindDependencyCyclesRequest, DependencyCyclesData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<DependencyCyclesData>> ExecuteCoreAsync(FindDependencyCyclesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (!context.ToolExecutionServices.DependencyAnalysisService.IsSupportedCycleGranularity(request.Granularity))
        {
            return PluginExecutionResult.Rejected<DependencyCyclesData>(PluginErrorCodes.InvalidRequest, "Granularity must be Project, Namespace, or Type.");
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

        var analysisResult = await context.ToolExecutionServices.DependencyAnalysisService.FindCyclesAsync(
            request.Granularity,
            projects.Value,
            documents.Value,
            request.EffectiveCyclesLimit,
            request.EffectiveNodesLimit,
            request.EffectiveEdgesLimit,
            context,
            cancellationToken);

        if (!analysisResult.IsCompleted)
        {
            var exceededLimit = analysisResult.Status == DependencyCycleAnalysisStatus.NodeLimitExceeded
                ? nameof(request.NodesLimit)
                : nameof(request.EdgesLimit);

            return PluginExecutionResult.Rejected<DependencyCyclesData>(
                PluginErrorCodes.AnalysisLimitExceeded,
                $"Dependency-cycle analysis exceeded {exceededLimit}. Narrow the scope or increase that limit.");
        }

        var data = new DependencyCyclesData
        {
            Cycles = BoundedCollection.CreatePrebounded(analysisResult.Cycles, analysisResult.TotalCount.Value),
        };

        return PluginExecutionResult.Success(data);
    }
}
