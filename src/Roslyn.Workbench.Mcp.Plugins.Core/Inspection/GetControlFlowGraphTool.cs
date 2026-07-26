namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-control-flow-graph", "Get Control Flow Graph", "Returns a projected control-flow graph for a symbol or selected region.")]
internal sealed class GetControlFlowGraphTool : QueryToolHandler<GetControlFlowGraphRequest, ControlFlowGraphData>
{
    protected override async ValueTask<PluginExecutionResult<ControlFlowGraphData>> ExecuteCoreAsync(GetControlFlowGraphRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (request.Symbol is not null && request.Location is not null)
        {
            return PluginExecutionResult.Rejected<ControlFlowGraphData>("InvalidRequest", "Specify exactly one of symbol or location.");
        }

        SyntaxNode node;
        SemanticModel semanticModel;
        ISymbol ownerSymbol;
        if (request.Symbol is not null)
        {
            var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<ControlFlowGraphData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
            if (symbolResolution.HasRejection)
            {
                return symbolResolution.Rejection;
            }

            ownerSymbol = symbolResolution.Value;
            var sourceLocation = ownerSymbol.Locations.FirstOrDefault(static item => item.IsInSource);
            if (sourceLocation is null || context.CurrentSolution.GetDocument(sourceLocation.SourceTree) is not { } document)
            {
                return PluginExecutionResult.Rejected<ControlFlowGraphData>("LocationNotFound", "The symbol does not have a source declaration.", RequiredAction.ResolveTargetAgain);
            }

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var resolvedSemanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (syntaxRoot is null || resolvedSemanticModel is null)
            {
                return PluginExecutionResult.Rejected<ControlFlowGraphData>("LocationNotFound", "The symbol source declaration could not be analysed.", RequiredAction.ResolveTargetAgain);
            }

            semanticModel = resolvedSemanticModel;
            node = syntaxRoot.FindNode(sourceLocation.SourceSpan, getInnermostNodeForTie: true);
        }
        else if (request.Location is { } location)
        {
            var syntaxNodeResolution = await ResolveSyntaxNodeAsync(location, request.ExpectedSnapshot, context, cancellationToken);
            if (syntaxNodeResolution.HasRejection)
            {
                return syntaxNodeResolution.Rejection;
            }

            node = syntaxNodeResolution.Value.Node;
            semanticModel = syntaxNodeResolution.Value.SemanticModel;
            var enclosingSymbol = semanticModel.GetEnclosingSymbol(node.SpanStart, cancellationToken);
            if (enclosingSymbol is null)
            {
                return PluginExecutionResult.Rejected<ControlFlowGraphData>("SymbolNotFound", "The selected location does not have an enclosing symbol.", RequiredAction.ResolveTargetAgain);
            }

            ownerSymbol = enclosingSymbol;
        }
        else
        {
            return PluginExecutionResult.Rejected<ControlFlowGraphData>("InvalidRequest", "Specify exactly one of symbol or location.");
        }

        var graph = ControlFlowGraph.Create(node, semanticModel, cancellationToken);
        if (graph is null)
        {
            return PluginExecutionResult.Rejected<ControlFlowGraphData>("InvalidRequest", "The selected target does not support control-flow graph generation.");
        }

        var maxBlocks = Math.Max(0, request.MaxBlocks);
        var blocks = CreateBlocks(graph, maxBlocks);
        var regions = CreateRegions(graph, Math.Max(0, request.MaxRegions), out var regionsTruncated);

        var data = new ControlFlowGraphData
        {
            Owner = context.WorkspaceResolver.CreateSymbolReference(ownerSymbol),
            Blocks = blocks,
            BlocksTruncated = blocks.Count < graph.Blocks.Length,
            Regions = regions,
            RegionsTruncated = regionsTruncated,
        };

        return PluginExecutionResult.Success(data);
    }

    private static List<BasicBlockInfo> CreateBlocks(ControlFlowGraph graph, int maxBlocks)
    {
        var blocks = new List<BasicBlockInfo>();
        foreach (var block in graph.Blocks)
        {
            if (blocks.Count == maxBlocks)
            {
                break;
            }

            var operations = new string[block.Operations.Length];
            for (var index = 0; index < block.Operations.Length; index++)
            {
                operations[index] = block.Operations[index].Syntax.ToString();
            }

            var blockInfo = new BasicBlockInfo
            {
                Ordinal = block.Ordinal,
                Kind = block.Kind.ToString(),
                IsReachable = block.IsReachable,
                Operations = operations,
                FallThroughSuccessor = block.FallThroughSuccessor?.Destination is { } fallThroughDestination ? fallThroughDestination.Ordinal : null,
                ConditionalSuccessor = block.ConditionalSuccessor?.Destination is { } conditionalDestination ? conditionalDestination.Ordinal : null,
            };

            blocks.Add(blockInfo);
        }

        return blocks;
    }

    private static List<FlowRegionInfo> CreateRegions(ControlFlowGraph graph, int maxRegions, out bool hasMore)
    {
        var regions = new List<FlowRegionInfo>();
        var nextId = 0;
        hasMore = !AddRegion(graph.Root, regions, maxRegions, ref nextId);
        return regions;
    }

    private static bool AddRegion(ControlFlowRegion region, ICollection<FlowRegionInfo> regions, int maxRegions, ref int nextId)
    {
        if (regions.Count == maxRegions)
        {
            return false;
        }

        regions.Add(new FlowRegionInfo
        {
            Id = nextId++,
            Kind = region.Kind.ToString(),
            FirstBlockOrdinal = region.FirstBlockOrdinal,
            LastBlockOrdinal = region.LastBlockOrdinal,
        });

        foreach (var nestedRegion in region.NestedRegions)
        {
            if (!AddRegion(nestedRegion, regions, maxRegions, ref nextId))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<ToolResolutionResult<ResolvedSyntaxNode, ControlFlowGraphData>> ResolveSyntaxNodeAsync(LocationSelector selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<ControlFlowGraphData>(context, expectedSnapshot);
        if (snapshotRejection is not null)
        {
            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, ControlFlowGraphData>(snapshotRejection);
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!locationResolution.IsResolved)
        {
            var rejection = SelectorRejectionFactory.Create<ControlFlowGraphData>(
                locationResolution.Status,
                "Location",
                "location");

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, ControlFlowGraphData>(rejection);
        }

        var location = locationResolution.Value;
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
        if (resolvedLocation?.Document?.Path is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, ControlFlowGraphData>(rejection);
        }

        var document = location.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(location.SourceTree);

        if (document is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, ControlFlowGraphData>(rejection);
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, ControlFlowGraphData>(rejection);
        }

        var node = syntaxRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
        var resolvedSyntaxNode = new ResolvedSyntaxNode(node, semanticModel);

        return ToolResolutionResult.Resolved<ResolvedSyntaxNode, ControlFlowGraphData>(resolvedSyntaxNode);
    }

    private static PluginExecutionResult<ControlFlowGraphData> CreateLocationNotFoundRejection()
    {
        return PluginExecutionResult.Rejected<ControlFlowGraphData>(
            "LocationNotFound",
            "The location selector did not resolve to a source document.",
            RequiredAction.ResolveTargetAgain);
    }

    private sealed record ResolvedSyntaxNode
    {
        public SyntaxNode Node { get; }

        public SemanticModel SemanticModel { get; }

        public ResolvedSyntaxNode(SyntaxNode node, SemanticModel semanticModel)
        {
            Node = node;
            SemanticModel = semanticModel;
        }
    }
}
