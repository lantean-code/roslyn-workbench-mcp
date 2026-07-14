using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-control-flow-graph", "Get Control Flow Graph", "Returns a projected control-flow graph for a symbol or selected region.")]
internal sealed class GetControlFlowGraphTool : QueryToolHandler<GetControlFlowGraphRequest, ControlFlowGraphData>
{
    protected override async ValueTask<PluginExecutionResult<ControlFlowGraphData>> ExecuteCoreAsync(GetControlFlowGraphRequest request, IQueryContext context, CancellationToken cancellationToken)
    {

        if (request.Symbol is null == request.Location is null)
        {
            return ToolExecutionHelpers.Rejected<ControlFlowGraphData>("InvalidRequest", "Specify exactly one of symbol or location.");
        }

        SyntaxNode node;
        SemanticModel semanticModel;
        ISymbol ownerSymbol;
        if (request.Symbol is not null)
        {
            var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<ControlFlowGraphData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
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

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var resolvedSemanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxRoot is null || resolvedSemanticModel is null)
            {
                return ToolExecutionHelpers.Rejected<ControlFlowGraphData>("LocationNotFound", "The symbol source declaration could not be analysed.", RequiredAction.ResolveTargetAgain);
            }

            semanticModel = resolvedSemanticModel;
            node = syntaxRoot.FindNode(sourceLocation.SourceSpan, getInnermostNodeForTie: true);
        }
        else
        {
            var location = request.Location
                ?? throw new InvalidOperationException("A validated control-flow graph request must contain a location.");
            var syntaxNodeResolution = await ResolveSyntaxNodeAsync(location, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
            if (syntaxNodeResolution.Rejection is not null)
            {
                return syntaxNodeResolution.Rejection;
            }

            if (syntaxNodeResolution.Node is null || syntaxNodeResolution.SemanticModel is null)
            {
                throw new InvalidOperationException("A successful syntax-node resolution must contain a node and semantic model.");
            }

            node = syntaxNodeResolution.Node;
            semanticModel = syntaxNodeResolution.SemanticModel;
            var enclosingSymbol = semanticModel.GetEnclosingSymbol(node.SpanStart, cancellationToken);
            if (enclosingSymbol is null)
            {
                return ToolExecutionHelpers.Rejected<ControlFlowGraphData>("SymbolNotFound", "The selected location does not have an enclosing symbol.", RequiredAction.ResolveTargetAgain);
            }

            ownerSymbol = enclosingSymbol;
        }

        var graph = ControlFlowGraph.Create(node, semanticModel, cancellationToken);
        if (graph is null)
        {
            return ToolExecutionHelpers.Rejected<ControlFlowGraphData>("InvalidRequest", "The selected target does not support control-flow graph generation.");
        }

        var blocks = graph.Blocks.Select(static block => new BasicBlockInfo
        {
            Ordinal = block.Ordinal,
            Kind = block.Kind.ToString(),
            IsReachable = block.IsReachable,
            Operations = block.Operations.Select(static operation => operation.Syntax.ToString()).ToArray(),
            FallThroughSuccessor = block.FallThroughSuccessor?.Destination is { } fallThroughDestination ? fallThroughDestination.Ordinal : null,
            ConditionalSuccessor = block.ConditionalSuccessor?.Destination is { } conditionalDestination ? conditionalDestination.Ordinal : null,
        }).ToArray();
        var regions = CreateRegions(graph).ToArray();
        var boundedBlocks = blocks.Take(request.MaxBlocks).ToArray();
        var boundedRegions = regions.Take(request.MaxRegions).ToArray();

        return PluginExecutionResult<ControlFlowGraphData>.Success(new ControlFlowGraphData
        {
            Owner = context.WorkspaceResolver.CreateSymbolReference(ownerSymbol),
            Blocks = boundedBlocks,
            BlocksTruncated = boundedBlocks.Length < blocks.Length,
            Regions = boundedRegions,
            RegionsTruncated = boundedRegions.Length < regions.Length,
        });
    }

    private static IReadOnlyList<FlowRegionInfo> CreateRegions(ControlFlowGraph graph)
    {
        var regions = new List<FlowRegionInfo>();
        var nextId = 0;
        AddRegion(graph.Root, regions, ref nextId);
        return regions;
    }

    private static void AddRegion(ControlFlowRegion region, ICollection<FlowRegionInfo> regions, ref int nextId)
    {
        regions.Add(new FlowRegionInfo
        {
            Id = nextId++,
            Kind = region.Kind.ToString(),
            FirstBlockOrdinal = region.FirstBlockOrdinal,
            LastBlockOrdinal = region.LastBlockOrdinal,
        });

        foreach (var nestedRegion in region.NestedRegions)
        {
            AddRegion(nestedRegion, regions, ref nextId);
        }
    }

    private static async ValueTask<SyntaxNodeResolution> ResolveSyntaxNodeAsync(LocationSelector selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var rejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<ControlFlowGraphData>(context, expectedSnapshot);
        if (rejection is not null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = rejection,
            };
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken).ConfigureAwait(false);
        if (!locationResolution.IsResolved)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<ControlFlowGraphData>(locationResolution.Status, "Location"),
            };
        }

        var location = locationResolution.Value;
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
        if (resolvedLocation?.Document?.Path is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<ControlFlowGraphData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var document = location.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(location.SourceTree);
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
            Node = syntaxRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true),
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
