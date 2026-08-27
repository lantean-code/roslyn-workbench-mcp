namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("analyze-control-flow", "Analyze Control Flow", "Analyzes control flow for an exact complete statement or contiguous statement range.")]
internal sealed class AnalyzeControlFlowTool : QueryToolHandler<AnalyzeControlFlowRequest, ControlFlowAnalysisData>
{
    protected override async ValueTask<PluginExecutionResult<ControlFlowAnalysisData>> ExecuteCoreAsync(AnalyzeControlFlowRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var regionResolution = await FlowAnalysisRegionResolver.ResolveStatementRegionAsync<ControlFlowAnalysisData>(
            request.Location,
            request.ExpectedSnapshot,
            context,
            cancellationToken);

        if (regionResolution.HasRejection)
        {
            return regionResolution.Rejection;
        }

        var resolvedRegion = regionResolution.Value;

        var analysis = resolvedRegion.SemanticModel.AnalyzeControlFlow(
            resolvedRegion.FirstStatement,
            resolvedRegion.LastStatement);
        if (analysis is null)
        {
            return PluginExecutionResult.Rejected<ControlFlowAnalysisData>("InvalidRequest", "The selected region does not support control-flow analysis.");
        }

        var exits = new List<ControlFlowExit>();
        foreach (var exitPoint in analysis.ExitPoints)
        {
            if (exits.Count == request.EffectiveExitsLimit)
            {
                break;
            }

            exits.Add(new ControlFlowExit
            {
                Kind = exitPoint.Kind().ToString(),
                Location = context.WorkspaceResolver.CreateResolvedLocation(exitPoint.GetLocation()),
            });
        }

        var returns = new List<ResolvedLocation>();
        foreach (var returnStatement in analysis.ReturnStatements)
        {
            if (returns.Count == request.EffectiveReturnsLimit)
            {
                break;
            }

            var returnLocation = context.WorkspaceResolver.CreateResolvedLocation(returnStatement.GetLocation());
            if (returnLocation is not null)
            {
                returns.Add(returnLocation);
            }
        }

        var data = new ControlFlowAnalysisData
        {
            Region = resolvedRegion.ResolvedLocation,
            EntryReachable = analysis.StartPointIsReachable,
            ExitReachable = analysis.EndPointIsReachable,
            Exits = BoundedCollection.CreatePrebounded(exits, analysis.ExitPoints.Length),
            Returns = BoundedCollection.CreatePrebounded(returns, analysis.ReturnStatements.Length),
        };

        return PluginExecutionResult.Success(data);
    }
}
