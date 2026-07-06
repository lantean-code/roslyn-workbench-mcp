using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class AnalyzeDataFlowTool : QueryToolHandler<AnalyzeDataFlowRequest, DataFlowAnalysisData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "analyze-data-flow",
        Title = "Analyze Data Flow",
        Description = "Analyzes data flow for a selected executable region.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new AnalyzeDataFlowTool());
    }

    protected override async ValueTask<PluginExecutionResult<DataFlowAnalysisData>> ExecuteCoreAsync(AnalyzeDataFlowRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var statementResolution = await ResolveStatementAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (statementResolution.Rejection is not null)
        {
            return statementResolution.Rejection;
        }

        var analysis = statementResolution.SemanticModel!.AnalyzeDataFlow(statementResolution.Statement!);
        if (analysis is null)
        {
            return ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("InvalidRequest", "The selected region does not support data-flow analysis.");
        }

        return context.ToolExecutionServices.ResultShaper.EnsureWithinSize(context, new DataFlowAnalysisData
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

        var statement = syntaxNodeResolution.Node!.FirstAncestorOrSelf<StatementSyntax>();
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
        if (locationResolution.Status != SelectorResolveStatus.Resolved)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<DataFlowAnalysisData>(locationResolution.Status, "Location"),
            };
        }

        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(locationResolution.Value!);
        if (resolvedLocation?.Document?.Path is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<DataFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var document = context.CurrentSolution.GetDocument(locationResolution.Value!.SourceTree!);
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
            Node = syntaxRoot.FindNode(locationResolution.Value.SourceSpan, getInnermostNodeForTie: true),
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
