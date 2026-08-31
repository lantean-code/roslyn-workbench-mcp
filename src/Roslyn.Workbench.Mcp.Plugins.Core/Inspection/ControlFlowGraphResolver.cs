namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Finds the Roslyn control-flow graph that owns a selected syntax node, including nested executable scopes.
/// </summary>
internal static class ControlFlowGraphResolver
{
    /// <summary>
    /// Resolves the control-flow graph containing the selected syntax node.
    /// </summary>
    /// <param name="node">The syntax node whose executable graph is required.</param>
    /// <param name="semanticModel">The semantic model for the node's syntax tree.</param>
    /// <param name="cancellationToken">The token that cancels semantic analysis.</param>
    /// <returns>The owning control-flow graph, or <see langword="null"/> when Roslyn cannot construct one.</returns>
    public static ControlFlowGraph? Resolve(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var operation = FindOperation(node, semanticModel, cancellationToken);
        if (operation is null)
        {
            return null;
        }

        var rootOperation = operation;
        while (rootOperation.Parent is not null)
        {
            rootOperation = rootOperation.Parent;
        }

        var graph = CreateRootControlFlowGraph(rootOperation, cancellationToken);
        if (graph is null)
        {
            return null;
        }

        var enclosingSymbol = semanticModel.GetEnclosingSymbol(node.SpanStart, cancellationToken);
        if (enclosingSymbol is not IMethodSymbol ownerMethod)
        {
            return graph;
        }

        var nestedScopes = GetNestedExecutableScopes(ownerMethod);
        foreach (var nestedScope in nestedScopes)
        {
            graph = ResolveNestedControlFlowGraph(graph, nestedScope, cancellationToken);
            if (graph is null)
            {
                return null;
            }
        }

        return graph;
    }

    private static IOperation? FindOperation(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
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

    private static ControlFlowGraph? CreateRootControlFlowGraph(IOperation operation, CancellationToken cancellationToken)
    {
        return operation switch
        {
            IAttributeOperation attribute => ControlFlowGraph.Create(attribute, cancellationToken),
            IConstructorBodyOperation constructorBody => ControlFlowGraph.Create(constructorBody, cancellationToken),
            IFieldInitializerOperation fieldInitializer => ControlFlowGraph.Create(fieldInitializer, cancellationToken),
            IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody, cancellationToken),
            IParameterInitializerOperation parameterInitializer => ControlFlowGraph.Create(parameterInitializer, cancellationToken),
            IPropertyInitializerOperation propertyInitializer => ControlFlowGraph.Create(propertyInitializer, cancellationToken),
            _ => null,
        };
    }

    private static IReadOnlyList<IMethodSymbol> GetNestedExecutableScopes(IMethodSymbol ownerMethod)
    {
        var nestedScopes = new Stack<IMethodSymbol>();
        IMethodSymbol? current = ownerMethod;
        while (current is { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
        {
            nestedScopes.Push(current);
            current = current.ContainingSymbol as IMethodSymbol;
        }

        return [.. nestedScopes];
    }

    private static ControlFlowGraph? ResolveNestedControlFlowGraph(ControlFlowGraph graph, IMethodSymbol nestedScope, CancellationToken cancellationToken)
    {
        if (nestedScope.MethodKind == MethodKind.LocalFunction)
        {
            var containsLocalFunction = graph.LocalFunctions.Any(item => SymbolEqualityComparer.Default.Equals(item, nestedScope));
            if (!containsLocalFunction)
            {
                return graph;
            }

            return graph.GetLocalFunctionControlFlowGraphInScope(nestedScope, cancellationToken);
        }

        var anonymousFunction = FindAnonymousFunctionInGraph(graph, nestedScope);
        if (anonymousFunction is null)
        {
            return graph;
        }

        return graph.GetAnonymousFunctionControlFlowGraphInScope(anonymousFunction, cancellationToken);
    }

    private static IFlowAnonymousFunctionOperation? FindAnonymousFunctionInGraph(ControlFlowGraph graph, IMethodSymbol anonymousFunctionSymbol)
    {
        foreach (var block in graph.Blocks)
        {
            foreach (var operation in block.Operations)
            {
                var anonymousFunction = FindAnonymousFunctionInOperation(operation, anonymousFunctionSymbol);
                if (anonymousFunction is not null)
                {
                    return anonymousFunction;
                }
            }

            if (block.BranchValue is { } branchValue)
            {
                var anonymousFunction = FindAnonymousFunctionInOperation(branchValue, anonymousFunctionSymbol);
                if (anonymousFunction is not null)
                {
                    return anonymousFunction;
                }
            }
        }

        return null;
    }

    private static IFlowAnonymousFunctionOperation? FindAnonymousFunctionInOperation(IOperation operation, IMethodSymbol anonymousFunctionSymbol)
    {
        foreach (var candidate in operation.DescendantsAndSelf())
        {
            if (candidate is IFlowAnonymousFunctionOperation anonymousFunction
                && SymbolEqualityComparer.Default.Equals(anonymousFunction.Symbol, anonymousFunctionSymbol))
            {
                return anonymousFunction;
            }
        }

        return null;
    }
}
