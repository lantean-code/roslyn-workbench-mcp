namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-test-impact", "Get Test Impact", "Returns likely impacted tests for a resolved symbol.")]
internal sealed class GetTestImpactTool : QueryToolHandler<GetTestImpactRequest, TestImpactData>
{
    protected override async ValueTask<PluginExecutionResult<TestImpactData>> ExecuteCoreAsync(GetTestImpactRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<TestImpactData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<TestImpactData>(request.TestScope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var symbol = symbolResolution.Value;
        var impactedTests = await context.ToolExecutionServices.DependencyAnalysisService.FindTestImpactsAsync(
            symbol,
            documents.Value,
            request.IncludeReasons,
            context,
            cancellationToken);

        return PluginExecutionResult<TestImpactData>.Success(new TestImpactData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Tests = ToolExecutionHelpers.CreateBoundedCollection(
                impactedTests,
                request.EffectiveTestsLimit),
        });
    }
}
