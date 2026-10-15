namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns likely impacted tests for a resolved symbol.
/// </summary>
[RoslynTool("get-test-impact", "Get Test Impact", "Suggests tests to inspect after changing a resolved symbol, using static direct references to the target or its owning type. This heuristic can include tests that use another member of that type and misses indirect helper-call dependencies; it is not a coverage report or a guarantee of the tests affected. Candidate methods are selected by type names ending in Test, Tests, Spec or Specs, or method names starting GIVEN_ or containing _THEN_Should; test attributes alone do not select candidates. testScope limits candidate documents or projects and defaults to the loaded solution. Returns symbol and a bounded tests collection with test symbols and locations; includeReasons defaults to false. testsLimit defaults to 100; check tests.hasMore for truncation. Does not execute tests.")]
internal sealed class GetTestImpactTool : QueryToolHandler<GetTestImpactRequest, TestImpactData>
{
    /// <inheritdoc/>
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
        var (impactedTests, hasMore) = await context.ToolExecutionServices.DependencyAnalysisService.FindTestImpactsAsync(
            symbol,
            documents.Value,
            request.IncludeReasons,
            request.EffectiveTestsLimit,
            context,
            cancellationToken);

        var data = new TestImpactData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Tests = BoundedCollection.CreatePrebounded(impactedTests, hasMore),
        };

        return PluginExecutionResult.Success(data);
    }
}
