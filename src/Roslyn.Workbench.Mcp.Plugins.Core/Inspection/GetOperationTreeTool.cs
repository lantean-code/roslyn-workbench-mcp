namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-operation-tree", "Get Operation Tree", "Returns a projected IOperation tree for a selected region.")]
internal sealed class GetOperationTreeTool : QueryToolHandler<GetOperationTreeRequest, OperationTreeData>
{
    protected override async ValueTask<PluginExecutionResult<OperationTreeData>> ExecuteCoreAsync(GetOperationTreeRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var syntaxNodeResolution = await ResolveSyntaxNodeAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken);
        if (syntaxNodeResolution.Rejection is not null)
        {
            return syntaxNodeResolution.Rejection;
        }

        if (syntaxNodeResolution.Node is null || syntaxNodeResolution.SemanticModel is null)
        {
            throw new InvalidOperationException("A successful syntax-node resolution must contain a node and semantic model.");
        }

        var resolvedNode = syntaxNodeResolution.Node;
        var resolvedSemanticModel = syntaxNodeResolution.SemanticModel;
        var operation = resolvedSemanticModel.GetOperation(resolvedNode, cancellationToken)
            ?? resolvedNode.ChildNodes().Select(child => resolvedSemanticModel.GetOperation(child, cancellationToken)).FirstOrDefault(static item => item is not null);
        if (operation is null)
        {
            return ToolExecutionHelpers.Rejected<OperationTreeData>("InvalidRequest", "The selected region does not resolve to an operation tree.");
        }

        var root = CreateOperationNode(operation, request.MaxDepth, depth: 0, out var truncated);
        var data = new OperationTreeData
        {
            Root = root,
            Truncated = truncated,
        };

        return PluginExecutionResult<OperationTreeData>.Success(data);
    }

    private static OperationNode CreateOperationNode(IOperation operation, int maxDepth, int depth, out bool truncated)
    {
        if (depth >= maxDepth)
        {
            var childEnumerator = operation.ChildOperations.GetEnumerator();
            truncated = childEnumerator.MoveNext();

            return CreateOperationNodeProjection(operation, truncated, []);
        }

        var children = new List<OperationNode>();
        foreach (var childOperation in operation.ChildOperations)
        {
            children.Add(CreateOperationNode(childOperation, maxDepth, depth + 1, out _));
        }

        truncated = false;

        return CreateOperationNodeProjection(operation, truncated, children.ToArray());
    }

    private static OperationNode CreateOperationNodeProjection(IOperation operation, bool truncated, OperationNode[] children)
    {
        return new OperationNode
        {
            Kind = operation.Kind.ToString(),
            Type = operation.Type?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            ConstantValue = operation.ConstantValue.HasValue ? operation.ConstantValue.Value?.ToString() : null,
            Syntax = operation.Syntax.ToString(),
            Truncated = truncated,
            Children = children,
        };
    }

    private static async ValueTask<SyntaxNodeResolution> ResolveSyntaxNodeAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var rejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<OperationTreeData>(context, expectedSnapshot);
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
                Rejection = ToolExecutionHelpers.Rejected<OperationTreeData>("InvalidRequest", "A location selector is required."),
            };
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!locationResolution.IsResolved)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<OperationTreeData>(locationResolution.Status, "Location", "location"),
            };
        }

        var location = locationResolution.Value;
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
        if (resolvedLocation?.Document?.Path is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<OperationTreeData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var document = location.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(location.SourceTree);
        if (document is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<OperationTreeData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
            };
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.Rejected<OperationTreeData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain),
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
        public PluginExecutionResult<OperationTreeData>? Rejection { get; init; }

        public SyntaxNode? Node { get; init; }

        public SemanticModel? SemanticModel { get; init; }
    }
}
