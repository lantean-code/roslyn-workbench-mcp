using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class AnalyzeControlFlowTool : QueryToolHandler<AnalyzeControlFlowRequest, ControlFlowAnalysisData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "analyze-control-flow",
        Title = "Analyze Control Flow",
        Description = "Analyzes control flow for a selected executable region.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new AnalyzeControlFlowTool());
    }

    protected override async ValueTask<PluginExecutionResult<ControlFlowAnalysisData>> ExecuteCoreAsync(AnalyzeControlFlowRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var statementResolution = await ResolveStatementAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (statementResolution.Rejection is not null)
        {
            return statementResolution.Rejection;
        }

        var analysis = statementResolution.SemanticModel!.AnalyzeControlFlow(statementResolution.Statement!);
        if (analysis is null)
        {
            return ToolExecutionHelpers.Rejected<ControlFlowAnalysisData>("InvalidRequest", "The selected region does not support control-flow analysis.");
        }

        return PluginExecutionResult<ControlFlowAnalysisData>.Success(new ControlFlowAnalysisData
        {
            Region = statementResolution.ResolvedLocation,
            EntryReachable = analysis.StartPointIsReachable,
            ExitReachable = analysis.EndPointIsReachable,
            Exits = analysis.ExitPoints.Select(node => new ControlFlowExit
            {
                Kind = node.Kind().ToString(),
                Location = context.WorkspaceResolver.CreateResolvedLocation(node.GetLocation()),
            }).ToArray(),
            Returns = analysis.ReturnStatements
                .Select(node => context.WorkspaceResolver.CreateResolvedLocation(node.GetLocation()))
                .Where(static item => item is not null)
                .Select(static item => item!)
                .ToArray(),
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
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowAnalysisData>("InvalidRequest", "The selected region must resolve to an executable statement."),
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
        var rejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<ControlFlowAnalysisData>(context, expectedSnapshot);
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
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowAnalysisData>("InvalidRequest", "A location selector is required."),
            };
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken).ConfigureAwait(false);
        if (locationResolution.Status != SelectorResolveStatus.Resolved)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<ControlFlowAnalysisData>(locationResolution.Status, "Location"),
            };
        }

        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(locationResolution.Value!);
        if (resolvedLocation?.Document?.Path is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var document = context.CurrentSolution.GetDocument(locationResolution.Value!.SourceTree!);
        if (document is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxRoot is null || semanticModel is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowAnalysisData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
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
        public PluginExecutionResult<ControlFlowAnalysisData>? Rejection { get; init; }

        public StatementSyntax? Statement { get; init; }

        public SemanticModel? SemanticModel { get; init; }

        public ResolvedLocation? ResolvedLocation { get; init; }
    }

    private sealed record SyntaxNodeResolution
    {
        public PluginExecutionResult<ControlFlowAnalysisData>? Rejection { get; init; }

        public SyntaxNode? Node { get; init; }

        public SemanticModel? SemanticModel { get; init; }

        public ResolvedLocation? ResolvedLocation { get; init; }
    }
}
