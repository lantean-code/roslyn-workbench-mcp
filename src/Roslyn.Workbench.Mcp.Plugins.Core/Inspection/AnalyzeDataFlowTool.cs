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
            Region = resolvedStatement.ResolvedLocation,
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

    private static async ValueTask<ToolResolutionResult<ResolvedStatement, DataFlowAnalysisData>> ResolveStatementAsync(LocationSelector selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var syntaxNodeResolution = await ResolveSyntaxNodeAsync(selector, expectedSnapshot, context, cancellationToken);
        if (syntaxNodeResolution.HasRejection)
        {
            return ToolResolutionResult.Rejected<ResolvedStatement, DataFlowAnalysisData>(syntaxNodeResolution.Rejection);
        }

        var resolvedSyntaxNode = syntaxNodeResolution.Value;
        var statement = resolvedSyntaxNode.Node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            var rejection = PluginExecutionResult.Rejected<DataFlowAnalysisData>(
                "InvalidRequest",
                "The selected region must resolve to an executable statement.");

            return ToolResolutionResult.Rejected<ResolvedStatement, DataFlowAnalysisData>(rejection);
        }

        var resolvedStatement = new ResolvedStatement(
            statement,
            resolvedSyntaxNode.SemanticModel,
            resolvedSyntaxNode.ResolvedLocation);

        return ToolResolutionResult.Resolved<ResolvedStatement, DataFlowAnalysisData>(resolvedStatement);
    }

    private static async ValueTask<ToolResolutionResult<ResolvedSyntaxNode, DataFlowAnalysisData>> ResolveSyntaxNodeAsync(LocationSelector selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<DataFlowAnalysisData>(context, expectedSnapshot);
        if (snapshotRejection is not null)
        {
            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, DataFlowAnalysisData>(snapshotRejection);
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!locationResolution.IsResolved)
        {
            var rejection = SelectorRejectionFactory.Create<DataFlowAnalysisData>(
                locationResolution.Status,
                "Location",
                "location");

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, DataFlowAnalysisData>(rejection);
        }

        var location = locationResolution.Value;
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
        if (resolvedLocation?.Document?.Path is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, DataFlowAnalysisData>(rejection);
        }

        var document = location.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(location.SourceTree);

        if (document is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, DataFlowAnalysisData>(rejection);
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, DataFlowAnalysisData>(rejection);
        }

        var node = syntaxRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
        var resolvedSyntaxNode = new ResolvedSyntaxNode(node, semanticModel, resolvedLocation);
        return ToolResolutionResult.Resolved<ResolvedSyntaxNode, DataFlowAnalysisData>(resolvedSyntaxNode);
    }

    private static PluginExecutionResult<DataFlowAnalysisData> CreateLocationNotFoundRejection()
    {
        return PluginExecutionResult.Rejected<DataFlowAnalysisData>(
            "LocationNotFound",
            "The location selector did not resolve to a source document.",
            RequiredAction.ResolveTargetAgain);
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
