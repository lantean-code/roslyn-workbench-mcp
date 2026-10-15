using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Analyzes data flow for an exact expression, complete statement, or contiguous statement range.
/// </summary>
[RoslynTool("analyze-data-flow", "Analyze Data Flow", "Returns Roslyn variable sets for declarations, reads, writes, captures and data flowing into or out of an exact expression, complete statement or contiguous sibling statements in one executable body. Use this when planning to extract or move selected code; use get-operation-tree for semantic operation structure, or analyze-control-flow for region reachability and exits. Echo the current expectedSnapshot and select exact syntax spans. Each variable category is limited independently to 50 symbols by default; check its hasMore before treating the category as complete.")]
internal sealed class AnalyzeDataFlowTool : QueryToolHandler<AnalyzeDataFlowRequest, DataFlowAnalysisData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<DataFlowAnalysisData>> ExecuteCoreAsync(AnalyzeDataFlowRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var regionResolution = await FlowAnalysisRegionResolver.ResolveDataFlowRegionAsync<DataFlowAnalysisData>(
            request.Location,
            request.ExpectedSnapshot,
            context,
            cancellationToken);

        if (regionResolution.HasRejection)
        {
            return regionResolution.Rejection;
        }

        var resolvedRegion = regionResolution.Value;

        DataFlowAnalysis? analysis;
        if (resolvedRegion is ResolvedExpressionFlowRegion expressionRegion)
        {
            analysis = expressionRegion.SemanticModel.AnalyzeDataFlow(expressionRegion.Expression);
        }
        else
        {
            var statementRegion = (ResolvedStatementFlowRegion)resolvedRegion;
            analysis = statementRegion.SemanticModel.AnalyzeDataFlow(
                statementRegion.FirstStatement,
                statementRegion.LastStatement);
        }

        if (analysis is null || !analysis.Succeeded)
        {
            return PluginExecutionResult.Rejected<DataFlowAnalysisData>(PluginErrorCodes.InvalidRequest, "The selected region does not support data-flow analysis.");
        }

        var maxResults = request.EffectiveSymbolsPerCategoryLimit;
        var variablesDeclared = CreateSymbolReferences(analysis.VariablesDeclared, maxResults, context.WorkspaceResolver);
        var readInside = CreateSymbolReferences(analysis.ReadInside, maxResults, context.WorkspaceResolver);
        var writtenInside = CreateSymbolReferences(analysis.WrittenInside, maxResults, context.WorkspaceResolver);
        var dataFlowsIn = CreateSymbolReferences(analysis.DataFlowsIn, maxResults, context.WorkspaceResolver);
        var dataFlowsOut = CreateSymbolReferences(analysis.DataFlowsOut, maxResults, context.WorkspaceResolver);
        var captured = CreateSymbolReferences(analysis.Captured, maxResults, context.WorkspaceResolver);
        var data = new DataFlowAnalysisData
        {
            Region = resolvedRegion.ResolvedLocation,
            VariablesDeclared = variablesDeclared,
            ReadInside = readInside,
            WrittenInside = writtenInside,
            DataFlowsIn = dataFlowsIn,
            DataFlowsOut = dataFlowsOut,
            Captured = captured,
        };

        return PluginExecutionResult.Success(data);
    }

    private static BoundedCollection<SymbolReference> CreateSymbolReferences(
        ImmutableArray<ISymbol> symbols,
        int maxResults,
        IWorkspaceResolver workspaceResolver)
    {
        var references = new List<SymbolReference>();
        foreach (var symbol in symbols)
        {
            if (references.Count == maxResults)
            {
                break;
            }

            references.Add(workspaceResolver.CreateSymbolReference(symbol));
        }

        return BoundedCollection.CreatePrebounded(references, symbols.Length);
    }
}
