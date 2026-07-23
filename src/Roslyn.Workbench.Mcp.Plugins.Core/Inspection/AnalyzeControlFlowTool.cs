namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("analyze-control-flow", "Analyze Control Flow", "Analyzes control flow for a selected executable region.")]
internal sealed class AnalyzeControlFlowTool : QueryToolHandler<AnalyzeControlFlowRequest, ControlFlowAnalysisData>
{
    protected override async ValueTask<PluginExecutionResult<ControlFlowAnalysisData>> ExecuteCoreAsync(AnalyzeControlFlowRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var statementResolution = await ResolveStatementAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken);
        if (statementResolution.HasRejection)
        {
            return statementResolution.Rejection;
        }

        var resolvedStatement = statementResolution.Value;

        var analysis = resolvedStatement.SemanticModel.AnalyzeControlFlow(resolvedStatement.Statement);
        if (analysis is null)
        {
            return PluginExecutionResultFactory.Rejected<ControlFlowAnalysisData>("InvalidRequest", "The selected region does not support control-flow analysis.");
        }

        var exits = new List<ControlFlowExit>();
        foreach (var exitPoint in analysis.ExitPoints)
        {
            exits.Add(new ControlFlowExit
            {
                Kind = exitPoint.Kind().ToString(),
                Location = context.WorkspaceResolver.CreateResolvedLocation(exitPoint.GetLocation()),
            });
        }

        var returns = new List<ResolvedLocation>();
        foreach (var returnStatement in analysis.ReturnStatements)
        {
            var returnLocation = context.WorkspaceResolver.CreateResolvedLocation(returnStatement.GetLocation());
            if (returnLocation is not null)
            {
                returns.Add(returnLocation);
            }
        }

        var data = new ControlFlowAnalysisData
        {
            Region = resolvedStatement.ResolvedLocation,
            EntryReachable = analysis.StartPointIsReachable,
            ExitReachable = analysis.EndPointIsReachable,
            Exits = exits,
            Returns = returns,
        };

        return PluginExecutionResult<ControlFlowAnalysisData>.Success(data);
    }

    private static async ValueTask<ToolResolutionResult<ResolvedStatement, ControlFlowAnalysisData>> ResolveStatementAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var syntaxNodeResolution = await ResolveSyntaxNodeAsync(selector, expectedSnapshot, context, cancellationToken);
        if (syntaxNodeResolution.HasRejection)
        {
            return ToolResolutionResult<ResolvedStatement, ControlFlowAnalysisData>.Rejected(syntaxNodeResolution.Rejection);
        }

        var resolvedSyntaxNode = syntaxNodeResolution.Value;
        var statement = resolvedSyntaxNode.Node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            return ToolResolutionResult<ResolvedStatement, ControlFlowAnalysisData>.Rejected(PluginExecutionResultFactory.Rejected<ControlFlowAnalysisData>("InvalidRequest", "The selected region must resolve to an executable statement."));
        }

        return ToolResolutionResult<ResolvedStatement, ControlFlowAnalysisData>.Resolved(new ResolvedStatement(
                statement,
                resolvedSyntaxNode.SemanticModel,
                resolvedSyntaxNode.ResolvedLocation));
    }

    private static async ValueTask<ToolResolutionResult<ResolvedSyntaxNode, ControlFlowAnalysisData>> ResolveSyntaxNodeAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var rejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<ControlFlowAnalysisData>(context, expectedSnapshot);
        if (rejection is not null)
        {
            return ToolResolutionResult<ResolvedSyntaxNode, ControlFlowAnalysisData>.Rejected(rejection);
        }

        if (selector is null)
        {
            return ToolResolutionResult<ResolvedSyntaxNode, ControlFlowAnalysisData>.Rejected(PluginExecutionResultFactory.Rejected<ControlFlowAnalysisData>("InvalidRequest", "A location selector is required."));
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!locationResolution.IsResolved)
        {
            return ToolResolutionResult<ResolvedSyntaxNode, ControlFlowAnalysisData>.Rejected(PluginExecutionResultFactory.RejectedFromStatus<ControlFlowAnalysisData>(locationResolution.Status, "Location", "location"));
        }

        var location = locationResolution.Value;
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
        if (resolvedLocation?.Document?.Path is null)
        {
            return ToolResolutionResult<ResolvedSyntaxNode, ControlFlowAnalysisData>.Rejected(PluginExecutionResultFactory.Rejected<ControlFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain));
        }

        var document = location.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(location.SourceTree);

        if (document is null)
        {
            return ToolResolutionResult<ResolvedSyntaxNode, ControlFlowAnalysisData>.Rejected(PluginExecutionResultFactory.Rejected<ControlFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain));
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            return ToolResolutionResult<ResolvedSyntaxNode, ControlFlowAnalysisData>.Rejected(PluginExecutionResultFactory.Rejected<ControlFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain));
        }

        return ToolResolutionResult<ResolvedSyntaxNode, ControlFlowAnalysisData>.Resolved(new ResolvedSyntaxNode(
                syntaxRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true),
                semanticModel,
                resolvedLocation));
    }

    private sealed record ResolvedStatement
    {
        public StatementSyntax Statement { get; }

        public SemanticModel SemanticModel { get; }

        public ResolvedLocation ResolvedLocation { get; }

        public ResolvedStatement(StatementSyntax statement, SemanticModel semanticModel, ResolvedLocation resolvedLocation)
        {
            Statement = statement;
            SemanticModel = semanticModel;
            ResolvedLocation = resolvedLocation;
        }
    }

    private sealed record ResolvedSyntaxNode
    {
        public SyntaxNode Node { get; }

        public SemanticModel SemanticModel { get; }

        public ResolvedLocation ResolvedLocation { get; }

        public ResolvedSyntaxNode(SyntaxNode node, SemanticModel semanticModel, ResolvedLocation resolvedLocation)
        {
            Node = node;
            SemanticModel = semanticModel;
            ResolvedLocation = resolvedLocation;
        }
    }
}
