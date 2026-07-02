using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class GetCodeMetricsTool : QueryToolHandler<GetCodeMetricsRequest, CodeMetricsData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-code-metrics",
        Title = "Get Code Metrics",
        Description = "Returns projected code metrics for a scope or symbol.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetCodeMetricsTool());
    }

    protected override async ValueTask<PluginExecutionResult<CodeMetricsData>> ExecuteCoreAsync(GetCodeMetricsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<MetricInfo> metrics;
        try
        {
            metrics = await CodeStructureAnalysisHelpers.GetMetricsAsync(request, context, cancellationToken).ConfigureAwait(false);
        }
        catch (CodeStructureAnalysisHelpers.MetricsResolutionException ex)
        {
            return ex.Rejection;
        }

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            metrics,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            static (items, hasMore) => new CodeMetricsData
            {
                Metrics = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }
}
