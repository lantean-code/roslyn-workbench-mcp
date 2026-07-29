namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-callees", "Find Callees", "Returns symbols directly invoked by a method or selected executable body.")]
internal sealed class FindCalleesTool : QueryToolHandler<FindCalleesRequest, CalleeSearchData>
{
    protected override async ValueTask<PluginExecutionResult<CalleeSearchData>> ExecuteCoreAsync(FindCalleesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (request.Symbol is null == request.Location is null)
        {
            return PluginExecutionResult.Rejected<CalleeSearchData>("InvalidRequest", "Specify exactly one of symbol or location.");
        }

        var directCallees = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        ISymbol sourceSymbol;

        if (request.Symbol is not null)
        {
            var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<CalleeSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
            if (symbolResolution.HasRejection)
            {
                return symbolResolution.Rejection;
            }

            sourceSymbol = symbolResolution.Value;
            var foundSourceOperation = false;
            foreach (var syntaxReference in sourceSymbol.DeclaringSyntaxReferences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var syntax = await syntaxReference.GetSyntaxAsync(cancellationToken);
                if (context.CurrentSolution.GetDocument(syntax.SyntaxTree) is not { } document)
                {
                    continue;
                }

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                if (semanticModel is null)
                {
                    continue;
                }

                var executableNode = GetExecutableNode(syntax);
                if (executableNode is null)
                {
                    continue;
                }

                var operation = semanticModel.GetOperation(executableNode, cancellationToken);
                if (operation is null)
                {
                    continue;
                }

                foundSourceOperation = true;
                AddDirectCallees(operation, directCallees);
            }

            if (!foundSourceOperation)
            {
                return PluginExecutionResult.Rejected<CalleeSearchData>("InvalidRequest", "The selected symbol does not have an executable source body.");
            }
        }
        else
        {
            var locationResolution = await ResolveLocationAsync(request.Location, request.ExpectedSnapshot, context, cancellationToken);
            if (locationResolution.HasRejection)
            {
                return locationResolution.Rejection;
            }

            var resolvedLocation = locationResolution.Value;

            var operation = GetOperation(resolvedLocation.SemanticModel, resolvedLocation.Node, cancellationToken);
            if (operation is null)
            {
                return PluginExecutionResult.Rejected<CalleeSearchData>("InvalidRequest", "The selected location does not resolve to executable code.");
            }

            var enclosingSymbol = resolvedLocation.SemanticModel.GetEnclosingSymbol(resolvedLocation.Node.SpanStart, cancellationToken);
            if (enclosingSymbol is null)
            {
                return PluginExecutionResult.Rejected<CalleeSearchData>("SymbolNotFound", "The selected location does not have an enclosing symbol.", RequiredAction.ResolveTargetAgain);
            }

            sourceSymbol = enclosingSymbol;
            AddDirectCallees(operation, directCallees);
        }

        if (request.IncludeIndirect)
        {
            await ExpandIndirectCalleesAsync(directCallees, request.MaxDepth, context, cancellationToken);
        }

        var orderedCallees = directCallees
            .Select(symbol => context.WorkspaceResolver.CreateSymbolReference(symbol))
            .OrderBy(static symbol => symbol.DisplayName, StringComparer.Ordinal);

        var callees = new List<SymbolReference>();
        foreach (var calleeReference in orderedCallees)
        {
            if (callees.Count == request.EffectiveCalleesLimit)
            {
                break;
            }

            callees.Add(calleeReference);
        }

        var source = context.WorkspaceResolver.CreateSymbolReference(sourceSymbol);
        var data = new CalleeSearchData
        {
            Source = source,
            Callees = BoundedCollection.CreatePrebounded(callees, directCallees.Count),
        };

        return PluginExecutionResult.Success(data);
    }

    private static void AddDirectCallees(IOperation operation, HashSet<ISymbol> callees)
    {
        foreach (var descendant in operation.DescendantsAndSelf())
        {
            switch (descendant)
            {
                case IInvocationOperation invocationOperation:
                    callees.Add(invocationOperation.TargetMethod);
                    break;

                case IObjectCreationOperation objectCreationOperation when objectCreationOperation.Constructor is not null:
                    callees.Add(objectCreationOperation.Constructor);
                    break;
            }
        }
    }

    private static async ValueTask ExpandIndirectCalleesAsync(HashSet<ISymbol> callees, int maxDepth, IQueryContext context, CancellationToken cancellationToken)
    {
        var visited = new HashSet<ISymbol>(callees, SymbolEqualityComparer.Default);
        var pending = new Queue<(ISymbol Symbol, int Depth)>(callees.Count);
        foreach (var callee in callees)
        {
            pending.Enqueue((callee, 1));
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (symbol, depth) = pending.Dequeue();
            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
            {
                var syntax = await syntaxReference.GetSyntaxAsync(cancellationToken);
                if (context.CurrentSolution.GetDocument(syntax.SyntaxTree) is not { } document)
                {
                    continue;
                }

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                if (semanticModel is null)
                {
                    continue;
                }

                var executableNode = GetExecutableNode(syntax);
                if (executableNode is null)
                {
                    continue;
                }

                var operation = semanticModel.GetOperation(executableNode, cancellationToken);
                if (operation is null)
                {
                    continue;
                }

                var nestedCallees = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                AddDirectCallees(operation, nestedCallees);
                foreach (var nested in nestedCallees)
                {
                    if (visited.Add(nested))
                    {
                        callees.Add(nested);
                        pending.Enqueue((nested, depth + 1));
                    }
                }
            }
        }
    }

    private static IOperation? GetOperation(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            var operation = semanticModel.GetOperation(current, cancellationToken);
            if (operation is not null)
            {
                return operation;
            }
        }

        return null;
    }

    private static CSharpSyntaxNode? GetExecutableNode(SyntaxNode node)
    {
        return node switch
        {
            BaseMethodDeclarationSyntax { Body: not null } method => method.Body,
            BaseMethodDeclarationSyntax { ExpressionBody: not null } method => method.ExpressionBody.Expression,
            LocalFunctionStatementSyntax { Body: not null } localFunction => localFunction.Body,
            LocalFunctionStatementSyntax { ExpressionBody: not null } localFunction => localFunction.ExpressionBody.Expression,
            AccessorDeclarationSyntax { Body: not null } accessor => accessor.Body,
            AccessorDeclarationSyntax { ExpressionBody: not null } accessor => accessor.ExpressionBody.Expression,
            AnonymousFunctionExpressionSyntax anonymousFunction => anonymousFunction.Body,
            _ => null,
        };
    }

    private static async ValueTask<ToolResolutionResult<ResolvedCalleeLocation, CalleeSearchData>> ResolveLocationAsync(LocationSelector? selector, SnapshotPrecondition? expectedSnapshot, IQueryContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<CalleeSearchData>(context, expectedSnapshot);
        if (snapshotRejection is not null)
        {
            return ToolResolutionResult.Rejected<ResolvedCalleeLocation, CalleeSearchData>(snapshotRejection);
        }

        if (selector is null)
        {
            var rejection = PluginExecutionResult.Rejected<CalleeSearchData>(
                "InvalidRequest",
                "A location selector is required.");

            return ToolResolutionResult.Rejected<ResolvedCalleeLocation, CalleeSearchData>(rejection);
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!location.IsResolved)
        {
            var rejection = SelectorRejectionFactory.Create<CalleeSearchData>(
                location.Status,
                "Location",
                "location");

            return ToolResolutionResult.Rejected<ResolvedCalleeLocation, CalleeSearchData>(rejection);
        }

        var sourceLocation = location.Value;
        var document = sourceLocation.SourceTree is null
            ? null
            : context.CurrentSolution.GetDocument(sourceLocation.SourceTree);

        if (document is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedCalleeLocation, CalleeSearchData>(rejection);
        }

        var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (syntaxRoot is null || semanticModel is null)
        {
            var rejection = CreateLocationNotFoundRejection();

            return ToolResolutionResult.Rejected<ResolvedCalleeLocation, CalleeSearchData>(rejection);
        }

        var node = syntaxRoot.FindNode(sourceLocation.SourceSpan, getInnermostNodeForTie: true);
        var resolvedLocation = new ResolvedCalleeLocation(node, semanticModel);
        return ToolResolutionResult.Resolved<ResolvedCalleeLocation, CalleeSearchData>(resolvedLocation);
    }

    private static PluginExecutionResult<CalleeSearchData> CreateLocationNotFoundRejection()
    {
        return PluginExecutionResult.Rejected<CalleeSearchData>(
            "LocationNotFound",
            "The location selector did not resolve to a source document.",
            RequiredAction.ResolveTargetAgain);
    }

    private sealed record ResolvedCalleeLocation
    {
        public SyntaxNode Node { get; }

        public SemanticModel SemanticModel { get; }

        public ResolvedCalleeLocation(SyntaxNode node, SemanticModel semanticModel)
        {
            Node = node;
            SemanticModel = semanticModel;
        }
    }
}
