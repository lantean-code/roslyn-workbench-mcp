using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class GetControlFlowGraphTool : QueryToolHandler<GetControlFlowGraphRequest, ControlFlowGraphData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-control-flow-graph",
        Title = "Get Control Flow Graph",
        Description = "Returns a projected control-flow graph for a symbol or selected region.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetControlFlowGraphTool());
    }

    protected override async ValueTask<PluginExecutionResult<ControlFlowGraphData>> ExecuteCoreAsync(GetControlFlowGraphRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Symbol is null == request.Location is null)
        {
            return ToolExecutionHelpers.Rejected<ControlFlowGraphData>("InvalidRequest", "Specify exactly one of symbol or location.");
        }

        SyntaxNode node;
        SemanticModel semanticModel;
        ISymbol ownerSymbol;
        if (request.Symbol is not null)
        {
            var symbolResolution = await ToolExecutionHelpers.ResolveSymbolAsync<ControlFlowGraphData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
            if (symbolResolution.HasRejection)
            {
                return symbolResolution.Rejection;
            }

            ownerSymbol = symbolResolution.Value;
            var sourceLocation = ownerSymbol.Locations.FirstOrDefault(static item => item.IsInSource);
            if (sourceLocation is null || context.CurrentSolution.GetDocument(sourceLocation.SourceTree) is not { } document)
            {
                return ToolExecutionHelpers.Rejected<ControlFlowGraphData>("LocationNotFound", "The symbol does not have a source declaration.", RequiredAction.ResolveTargetAgain);
            }

            semanticModel = (await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false))!;
            node = (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false))!.FindNode(sourceLocation.SourceSpan, getInnermostNodeForTie: true);
        }
        else
        {
            var syntaxNodeResolution = await ResolveSyntaxNodeAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
            if (syntaxNodeResolution.Rejection is not null)
            {
                return syntaxNodeResolution.Rejection;
            }

            node = syntaxNodeResolution.Node!;
            semanticModel = syntaxNodeResolution.SemanticModel!;
            ownerSymbol = semanticModel.GetEnclosingSymbol(node.SpanStart, cancellationToken)!;
        }

        var graph = ControlFlowGraph.Create(node, semanticModel, cancellationToken);
        if (graph is null)
        {
            return ToolExecutionHelpers.Rejected<ControlFlowGraphData>("InvalidRequest", "The selected target does not support control-flow graph generation.");
        }

        return ToolExecutionHelpers.EnsureWithinSize(context, new ControlFlowGraphData
        {
            Owner = context.Resolver.CreateSymbolReference(ownerSymbol),
            Blocks = graph.Blocks.Select(static block => new BasicBlockInfo
            {
                Ordinal = block.Ordinal,
                Kind = block.Kind.ToString(),
                IsReachable = block.IsReachable,
                Operations = block.Operations.Select(static operation => operation.Syntax.ToString()).ToArray(),
                FallThroughSuccessor = block.FallThroughSuccessor?.Destination is { } fallThroughDestination ? fallThroughDestination.Ordinal : null,
                ConditionalSuccessor = block.ConditionalSuccessor?.Destination is { } conditionalDestination ? conditionalDestination.Ordinal : null,
            }).ToArray(),
            Regions = [],
        });
    }

    private static async ValueTask<SyntaxNodeResolution> ResolveSyntaxNodeAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var rejection = ToolExecutionHelpers.ValidateSnapshot<ControlFlowGraphData>(context, expectedSnapshot);
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
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowGraphData>("InvalidRequest", "A location selector is required."),
            };
        }

        var locationResolution = await context.Resolver.ResolveLocationAsync(selector, cancellationToken).ConfigureAwait(false);
        if (locationResolution.Status != SelectorResolveStatus.Resolved)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<ControlFlowGraphData>(locationResolution.Status, "Location"),
            };
        }

        var resolvedLocation = context.Resolver.CreateResolvedLocation(locationResolution.Value!);
        if (resolvedLocation?.Document?.Path is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowGraphData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var document = context.CurrentSolution.GetDocument(locationResolution.Value!.SourceTree!);
        if (document is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowGraphData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxRoot is null || semanticModel is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowGraphData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        return new SyntaxNodeResolution
        {
            Node = syntaxRoot.FindNode(locationResolution.Value.SourceSpan, getInnermostNodeForTie: true),
            SemanticModel = semanticModel,
        };
    }

    private sealed record SyntaxNodeResolution
    {
        public PluginExecutionResult<ControlFlowGraphData>? Rejection { get; init; }

        public SyntaxNode? Node { get; init; }

        public SemanticModel? SemanticModel { get; init; }
    }
}
