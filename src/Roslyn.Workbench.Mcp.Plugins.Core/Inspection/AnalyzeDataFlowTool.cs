namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("analyze-data-flow", "Analyze Data Flow", "Analyzes data flow for a selected executable region.")]
internal sealed class AnalyzeDataFlowTool : QueryToolHandler<AnalyzeDataFlowRequest, DataFlowAnalysisData>
{
    protected override async ValueTask<PluginExecutionResult<DataFlowAnalysisData>> ExecuteCoreAsync(AnalyzeDataFlowRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var statementResolution = await ResolveStatementAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken);
        if (statementResolution.HasRejection)
        {
            return statementResolution.Rejection;
        }

        var resolvedStatement = statementResolution.Value;

        var analysis = resolvedStatement.SemanticModel.AnalyzeDataFlow(resolvedStatement.Statement);
        if (analysis is null)
        {
            return ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("InvalidRequest", "The selected region does not support data-flow analysis.");
        }

        return PluginExecutionResult<DataFlowAnalysisData>.Success(new DataFlowAnalysisData
        {
            Region = resolvedStatement.ResolvedLocation,
            VariablesDeclared = analysis.VariablesDeclared.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            ReadInside = analysis.ReadInside.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            WrittenInside = analysis.WrittenInside.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            DataFlowsIn = analysis.DataFlowsIn.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            DataFlowsOut = analysis.DataFlowsOut.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            Captured = analysis.Captured.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
        });
    }

    private static async ValueTask<ToolResolutionResult<ResolvedStatement, DataFlowAnalysisData>> ResolveStatementAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var syntaxNodeResolution = await ResolveSyntaxNodeAsync(selector, expectedSnapshot, context, cancellationToken);
        if (syntaxNodeResolution.HasRejection)
        {
            return new ToolResolutionResult<ResolvedStatement, DataFlowAnalysisData>
            {
                Rejection = syntaxNodeResolution.Rejection,
            };
        }

        var resolvedSyntaxNode = syntaxNodeResolution.Value;
        var statement = resolvedSyntaxNode.Node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            return new ToolResolutionResult<ResolvedStatement, DataFlowAnalysisData>
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("InvalidRequest", "The selected region must resolve to an executable statement."),
            };
        }

        return new ToolResolutionResult<ResolvedStatement, DataFlowAnalysisData>
        {
            Value = new ResolvedStatement(
                statement,
                resolvedSyntaxNode.SemanticModel,
                resolvedSyntaxNode.ResolvedLocation),
        };
    }

    private static async ValueTask<ToolResolutionResult<ResolvedSyntaxNode, DataFlowAnalysisData>> ResolveSyntaxNodeAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var rejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<DataFlowAnalysisData>(context, expectedSnapshot);
        if (rejection is not null)
        {
            return new ToolResolutionResult<ResolvedSyntaxNode, DataFlowAnalysisData>
            {
                Rejection = rejection,
            };
        }

        if (selector is null)
        {
            return new ToolResolutionResult<ResolvedSyntaxNode, DataFlowAnalysisData>
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("InvalidRequest", "A location selector is required."),
            };
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!locationResolution.IsResolved)
        {
            return new ToolResolutionResult<ResolvedSyntaxNode, DataFlowAnalysisData>
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<DataFlowAnalysisData>(locationResolution.Status, "Location", "location"),
            };
        }

        var location = locationResolution.Value;
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
        if (resolvedLocation?.Document?.Path is null)
        {
            return new ToolResolutionResult<ResolvedSyntaxNode, DataFlowAnalysisData>
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var document = location.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(location.SourceTree);
        if (document is null)
        {
            return new ToolResolutionResult<ResolvedSyntaxNode, DataFlowAnalysisData>
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            return new ToolResolutionResult<ResolvedSyntaxNode, DataFlowAnalysisData>
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        return new ToolResolutionResult<ResolvedSyntaxNode, DataFlowAnalysisData>
        {
            Value = new ResolvedSyntaxNode(
                syntaxRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true),
                semanticModel,
                resolvedLocation),
        };
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
