namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-operation-tree", "Get Operation Tree", "Returns a projected IOperation tree for a selected region.")]
internal sealed class GetOperationTreeTool : QueryToolHandler<GetOperationTreeRequest, OperationTreeData>
{
    protected override async ValueTask<PluginExecutionResult<OperationTreeData>> ExecuteCoreAsync(GetOperationTreeRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var syntaxNodeResolution = await ResolveSyntaxNodeAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
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
        return PluginExecutionResult<OperationTreeData>.Success(new OperationTreeData
        {
            Root = root,
            Truncated = truncated,
        });
    }

    private static OperationNode CreateOperationNode(IOperation operation, int maxDepth, int depth, out bool truncated)
    {
        var childOperations = operation.ChildOperations.ToArray();
        truncated = depth >= maxDepth && childOperations.Length > 0;
        var children = depth >= maxDepth
            ? []
            : childOperations.Select(child => CreateOperationNode(child, maxDepth, depth + 1, out _)).ToArray();

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

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken).ConfigureAwait(false);
        if (!locationResolution.IsResolved)
        {
            return new SyntaxNodeResolution
            {
                Rejection = ToolExecutionHelpers.RejectFromStatus<OperationTreeData>(locationResolution.Status, "Location"),
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

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
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
