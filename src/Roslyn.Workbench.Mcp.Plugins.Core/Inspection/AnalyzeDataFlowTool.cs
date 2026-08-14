namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("analyze-data-flow", "Analyze Data Flow", "Analyzes data flow for an exact expression, complete statement, or contiguous statement range.")]
internal sealed class AnalyzeDataFlowTool : QueryToolHandler<AnalyzeDataFlowRequest, DataFlowAnalysisData>
{
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
            return PluginExecutionResult.Rejected<DataFlowAnalysisData>("InvalidRequest", "The selected region does not support data-flow analysis.");
        }

        var variablesDeclared = CreateSymbolReferences(analysis.VariablesDeclared, context.WorkspaceResolver);
        var readInside = CreateSymbolReferences(analysis.ReadInside, context.WorkspaceResolver);
        var writtenInside = CreateSymbolReferences(analysis.WrittenInside, context.WorkspaceResolver);
        var dataFlowsIn = CreateSymbolReferences(analysis.DataFlowsIn, context.WorkspaceResolver);
        var dataFlowsOut = CreateSymbolReferences(analysis.DataFlowsOut, context.WorkspaceResolver);
        var captured = CreateSymbolReferences(analysis.Captured, context.WorkspaceResolver);
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

    private static List<SymbolReference> CreateSymbolReferences(IEnumerable<ISymbol> symbols, IWorkspaceResolver workspaceResolver)
    {
        var references = new List<SymbolReference>();
        foreach (var symbol in symbols)
        {
            references.Add(workspaceResolver.CreateSymbolReference(symbol));
        }

        return references;
    }

}
