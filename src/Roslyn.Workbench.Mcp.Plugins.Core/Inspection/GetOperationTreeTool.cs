namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-operation-tree", "Get Operation Tree", "Returns a bounded IOperation tree with metadata and exact source pointers.")]
internal sealed class GetOperationTreeTool : QueryToolHandler<GetOperationTreeRequest, OperationTreeData>
{
    protected override async ValueTask<PluginExecutionResult<OperationTreeData>> ExecuteCoreAsync(GetOperationTreeRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var syntaxNodeResolution = await ResolveSyntaxNodeAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken);
        if (syntaxNodeResolution.HasRejection)
        {
            return syntaxNodeResolution.Rejection;
        }

        var (resolvedNode, resolvedSemanticModel) = syntaxNodeResolution.Value;
        var operation = resolvedSemanticModel.GetOperation(resolvedNode, cancellationToken);
        if (operation is null)
        {
            foreach (var childNode in resolvedNode.ChildNodes())
            {
                operation = resolvedSemanticModel.GetOperation(childNode, cancellationToken);
                if (operation is not null)
                {
                    break;
                }
            }
        }

        if (operation is null)
        {
            return PluginExecutionResult.Rejected<OperationTreeData>("InvalidRequest", "The selected region does not resolve to an operation tree.");
        }

        OperationNode? root = null;
        var truncated = request.EffectiveNodesLimit == 0;
        if (!truncated)
        {
            var projectedNodeCount = 0;
            root = CreateOperationNode(
                operation,
                context.WorkspaceResolver,
                request.MaxDepth,
                request.EffectiveNodesLimit,
                depth: 0,
                ref projectedNodeCount,
                out truncated,
                cancellationToken);
        }

        var data = new OperationTreeData
        {
            Root = root,
            Truncated = truncated,
        };

        return PluginExecutionResult.Success(data);
    }

    private static OperationNode CreateOperationNode(IOperation operation, IWorkspaceResolver workspaceResolver, int maxDepth, int maxNodes, int depth, ref int projectedNodeCount, out bool truncated, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        projectedNodeCount++;
        if (depth >= maxDepth)
        {
            var childEnumerator = operation.ChildOperations.GetEnumerator();
            truncated = childEnumerator.MoveNext();

            return CreateOperationNodeProjection(operation, workspaceResolver, truncated, []);
        }

        var children = new List<OperationNode>();
        truncated = false;
        foreach (var childOperation in operation.ChildOperations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (projectedNodeCount == maxNodes)
            {
                truncated = true;
                break;
            }

            var child = CreateOperationNode(
                childOperation,
                workspaceResolver,
                maxDepth,
                maxNodes,
                depth + 1,
                ref projectedNodeCount,
                out var childTruncated,
                cancellationToken);

            children.Add(child);
            truncated |= childTruncated;
        }

        return CreateOperationNodeProjection(operation, workspaceResolver, truncated, children.ToArray());
    }

    private static OperationNode CreateOperationNodeProjection(IOperation operation, IWorkspaceResolver workspaceResolver, bool truncated, OperationNode[] children)
    {
        return new OperationNode
        {
            Kind = operation.Kind.ToString(),
            Type = operation.Type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            HasConstantValue = operation.ConstantValue.HasValue,
            Location = workspaceResolver.CreateResolvedLocation(operation.Syntax.GetLocation()),
            Truncated = truncated,
            Children = children,
        };
    }

    private static async ValueTask<ToolResolutionResult<ResolvedSyntaxNode, OperationTreeData>> ResolveSyntaxNodeAsync(LocationSelector selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<OperationTreeData>(context, expectedSnapshot);
        if (snapshotRejection is not null)
        {
            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, OperationTreeData>(snapshotRejection);
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!locationResolution.IsResolved)
        {
            var rejection = SelectorRejectionFactory.Create<OperationTreeData>(
                locationResolution.Status,
                "Location",
                "location");

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, OperationTreeData>(rejection);
        }

        var location = locationResolution.Value;
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
        if (resolvedLocation?.Document?.Path is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, OperationTreeData>(rejection);
        }

        var document = location.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(location.SourceTree);

        if (document is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, OperationTreeData>(rejection);
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedSyntaxNode, OperationTreeData>(rejection);
        }

        var node = syntaxRoot.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
        var resolvedSyntaxNode = new ResolvedSyntaxNode(node, semanticModel);
        return ToolResolutionResult.Resolved<ResolvedSyntaxNode, OperationTreeData>(resolvedSyntaxNode);
    }

    private static PluginExecutionResult<OperationTreeData> CreateLocationNotFoundRejection()
    {
        return PluginExecutionResult.Rejected<OperationTreeData>(
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

        public void Deconstruct(out SyntaxNode node, out SemanticModel semanticModel)
        {
            node = Node;
            semanticModel = SemanticModel;
        }
    }
}
