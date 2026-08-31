namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns a bounded control-flow graph with operation metadata and exact source pointers.
/// </summary>
[RoslynTool("get-control-flow-graph", "Get Control Flow Graph", "Returns a bounded control-flow graph with operation metadata and exact source pointers.")]
internal sealed class GetControlFlowGraphTool : QueryToolHandler<GetControlFlowGraphRequest, ControlFlowGraphData>
{
    /// <inheritdoc/>
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

        var graph = ControlFlowGraphResolver.Resolve(node, semanticModel, cancellationToken);
        if (graph is null)
        {
            return PluginExecutionResult.Rejected<ControlFlowGraphData>("InvalidRequest", "The selected target does not support control-flow graph generation.");
        }

        var blocks = CreateBlocks(
            graph,
            request.EffectiveMaxBlocks,
            request.EffectiveMaxOperationsPerBlock,
            context.WorkspaceResolver,
            cancellationToken);
        var regions = CreateRegions(graph, request.EffectiveMaxRegions);

        var data = new ControlFlowGraphData
        {
            Owner = context.WorkspaceResolver.CreateSymbolReference(ownerSymbol),
            Blocks = blocks,
            Regions = regions,
        };

        return PluginExecutionResult.Success(data);
    }

    private static BoundedCollection<BasicBlockInfo> CreateBlocks(
        ControlFlowGraph graph,
        int maxBlocks,
        int maxOperationsPerBlock,
        IWorkspaceResolver workspaceResolver,
        CancellationToken cancellationToken)
    {
        var blocks = new List<BasicBlockInfo>();
        foreach (var block in graph.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (blocks.Count == maxBlocks)
            {
                break;
            }

            var operations = new List<BasicBlockOperationInfo>();
            foreach (var operation in block.Operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (operations.Count == maxOperationsPerBlock)
                {
                    break;
                }

                operations.Add(new BasicBlockOperationInfo
                {
                    Kind = operation.Kind.ToString(),
                    Type = operation.Type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    Location = workspaceResolver.CreateResolvedLocation(operation.Syntax.GetLocation()),
                });
            }

            var blockInfo = new BasicBlockInfo
            {
                Ordinal = block.Ordinal,
                Kind = block.Kind.ToString(),
                IsReachable = block.IsReachable,
                Operations = BoundedCollection.CreatePrebounded(operations, block.Operations.Length),
                FallThroughSuccessor = block.FallThroughSuccessor?.Destination is { } fallThroughDestination ? fallThroughDestination.Ordinal : null,
                ConditionalSuccessor = block.ConditionalSuccessor?.Destination is { } conditionalDestination ? conditionalDestination.Ordinal : null,
            };

            blocks.Add(blockInfo);
        }

        return BoundedCollection.CreatePrebounded(blocks, graph.Blocks.Length);
    }

    private static BoundedCollection<FlowRegionInfo> CreateRegions(ControlFlowGraph graph, int maxRegions)
    {
        var regions = new List<FlowRegionInfo>();
        var nextId = 0;
        var hasMore = !AddRegion(graph.Root, regions, maxRegions, ref nextId);
        return BoundedCollection.CreatePrebounded(regions, hasMore);
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
