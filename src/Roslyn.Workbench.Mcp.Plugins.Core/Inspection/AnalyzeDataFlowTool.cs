namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("analyze-data-flow", "Analyze Data Flow", "Analyzes data flow for a selected executable region.")]
internal sealed class AnalyzeDataFlowTool : QueryToolHandler<AnalyzeDataFlowRequest, DataFlowAnalysisData>
{
    protected override async ValueTask<PluginExecutionResult<DataFlowAnalysisData>> ExecuteCoreAsync(AnalyzeDataFlowRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var statementResolution = await ResolveStatementAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (statementResolution.Rejection is not null)
        {
            return statementResolution.Rejection;
        }

        if (statementResolution.SemanticModel is null || statementResolution.Statement is null)
        {
            throw new InvalidOperationException("A successful statement resolution must contain a statement and semantic model.");
        }

        var analysis = statementResolution.SemanticModel.AnalyzeDataFlow(statementResolution.Statement);
        if (analysis is null)
        {
            return ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("InvalidRequest", "The selected region does not support data-flow analysis.");
        }

        return PluginExecutionResult<DataFlowAnalysisData>.Success(new DataFlowAnalysisData
        {
            Region = statementResolution.ResolvedLocation,
            VariablesDeclared = analysis.VariablesDeclared.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            ReadInside = analysis.ReadInside.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            WrittenInside = analysis.WrittenInside.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            DataFlowsIn = analysis.DataFlowsIn.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            DataFlowsOut = analysis.DataFlowsOut.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
            Captured = analysis.Captured.Select(context.WorkspaceResolver.CreateSymbolReference).ToArray(),
        });
    }

    private static async ValueTask<StatementResolution> ResolveStatementAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var syntaxNodeResolution = await ResolveSyntaxNodeAsync(selector, expectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (syntaxNodeResolution.Rejection is not null)
        {
            return new StatementResolution
            {
                Rejection = syntaxNodeResolution.Rejection,
            };
        }

        if (syntaxNodeResolution.Node is null)
        {
            throw new InvalidOperationException("A successful syntax-node resolution must contain a node.");
        }

        var statement = syntaxNodeResolution.Node.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            return new StatementResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("InvalidRequest", "The selected region must resolve to an executable statement."),
            };
        }

        return new StatementResolution
        {
            Statement = statement,
            SemanticModel = syntaxNodeResolution.SemanticModel,
            ResolvedLocation = syntaxNodeResolution.ResolvedLocation,
        };
    }

    private static async ValueTask<SyntaxNodeResolution> ResolveSyntaxNodeAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var rejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<DataFlowAnalysisData>(context, expectedSnapshot);
        if (rejection is not null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = rejection,
            };
        }

        if (selector is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("InvalidRequest", "A location selector is required."),
            };
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken).ConfigureAwait(false);
        if (!locationResolution.IsResolved)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<DataFlowAnalysisData>(locationResolution.Status, "Location", "location"),
            };
        }

        var location = locationResolution.Value;
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
        if (resolvedLocation?.Document?.Path is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var document = location.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(location.SourceTree);
        if (document is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxRoot is null || semanticModel is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        return new SyntaxNodeResolution
        {
            Node = syntaxRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true),
            SemanticModel = semanticModel,
            ResolvedLocation = resolvedLocation,
        };
    }

    private sealed record StatementResolution
    {
        public PluginExecutionResult<DataFlowAnalysisData>? Rejection { get; init; }

        public StatementSyntax? Statement { get; init; }

        public SemanticModel? SemanticModel { get; init; }

        public ResolvedLocation? ResolvedLocation { get; init; }
    }

    private sealed record SyntaxNodeResolution
    {
        public PluginExecutionResult<DataFlowAnalysisData>? Rejection { get; init; }

        public SyntaxNode? Node { get; init; }

        public SemanticModel? SemanticModel { get; init; }

        public ResolvedLocation? ResolvedLocation { get; init; }
    }
}
