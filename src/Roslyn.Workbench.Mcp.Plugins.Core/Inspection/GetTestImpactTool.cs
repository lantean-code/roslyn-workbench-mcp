using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GetTestImpactTool : QueryToolHandler<GetTestImpactRequest, TestImpactData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-test-impact",
        Title = "Get Test Impact",
        Description = "Returns likely impacted tests for a resolved symbol.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetTestImpactTool());
    }

    protected override async ValueTask<PluginExecutionResult<TestImpactData>> ExecuteCoreAsync(GetTestImpactRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<TestImpactData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var documents = ToolExecutionHelpers.ResolveDocuments<TestImpactData>(request.TestScope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var symbol = symbolResolution.Value;
        var impactedTests = await DependencyAnalysisHelpers.FindTestImpactsAsync(
            symbol,
            documents.Value,
            request.IncludeReasons,
            context,
            cancellationToken).ConfigureAwait(false);

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            impactedTests,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            (items, hasMore) => new TestImpactData
            {
                Symbol = context.Resolver.CreateSymbolReference(symbol),
                Tests = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }
}
