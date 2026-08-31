namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns the direct symbols used by a resolved symbol.
/// </summary>
[RoslynTool("get-symbol-dependencies", "Get Symbol Dependencies", "Returns the direct symbols used by a resolved symbol.")]
internal sealed class GetSymbolDependenciesTool : QueryToolHandler<GetSymbolDependenciesRequest, SymbolDependenciesData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<SymbolDependenciesData>> ExecuteCoreAsync(GetSymbolDependenciesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<SymbolDependenciesData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var dependencies = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        AddSignatureDependencies(symbol, dependencies);

        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
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

            AddOperationDependencies(semanticModel, syntax, dependencies, cancellationToken);
        }

        dependencies.RemoveWhere(dependency => SymbolEqualityComparer.Default.Equals(dependency, symbol));
        dependencies.RemoveWhere(static dependency => string.IsNullOrWhiteSpace(dependency.Name));

        var orderedDependencies = dependencies
            .Select(item => (Symbol: item, Reference: context.WorkspaceResolver.CreateSymbolReference(item)))
            .OrderBy(static item => item.Reference.DisplayName, StringComparer.Ordinal);

        var projectedDependencies = new List<DependencyInfo>();
        foreach (var (dependency, dependencyReference) in orderedDependencies)
        {
            if (projectedDependencies.Count == request.EffectiveDependenciesLimit)
            {
                break;
            }

            projectedDependencies.Add(new DependencyInfo
            {
                Symbol = dependencyReference,
                Kind = dependency.Kind.ToString(),
                AssemblyName = request.IncludeAssemblies ? dependency.ContainingAssembly?.Name : null,
            });
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new SymbolDependenciesData
        {
            Symbol = symbolReference,
            Dependencies = BoundedCollection.CreatePrebounded(projectedDependencies, dependencies.Count),
        };

        return PluginExecutionResult.Success(data);
    }

    private static void AddSignatureDependencies(ISymbol symbol, ISet<ISymbol> dependencies)
    {
        switch (symbol)
        {
            case IMethodSymbol methodSymbol:
                AddTypeSymbol(methodSymbol.ReturnType, dependencies);
                foreach (var parameter in methodSymbol.Parameters)
                {
                    AddTypeSymbol(parameter.Type, dependencies);
                }

                break;

            case IPropertySymbol propertySymbol:
                AddTypeSymbol(propertySymbol.Type, dependencies);
                break;

            case IFieldSymbol fieldSymbol:
                AddTypeSymbol(fieldSymbol.Type, dependencies);
                break;

            case INamedTypeSymbol namedTypeSymbol:
                AddTypeSymbol(namedTypeSymbol.BaseType, dependencies);
                foreach (var interfaceSymbol in namedTypeSymbol.Interfaces)
                {
                    AddTypeSymbol(interfaceSymbol, dependencies);
                }

                break;
        }
    }

    private static void AddOperationDependencies(SemanticModel semanticModel, SyntaxNode syntax, HashSet<ISymbol> dependencies, CancellationToken cancellationToken)
    {
        var executableNode = GetExecutableNode(syntax);
        var operationNode = executableNode ?? syntax;
        var rootOperation = semanticModel.GetOperation(operationNode, cancellationToken);
        if (rootOperation is null)
        {
            return;
        }

        foreach (var operation in rootOperation.DescendantsAndSelf())
        {
            AddTypeSymbol(operation.Type, dependencies);

            switch (operation)
            {
                case IInvocationOperation invocationOperation:
                    dependencies.Add(invocationOperation.TargetMethod);
                    break;

                case IObjectCreationOperation objectCreationOperation when objectCreationOperation.Constructor is not null:
                    dependencies.Add(objectCreationOperation.Constructor);
                    break;

                case IPropertyReferenceOperation propertyReferenceOperation:
                    dependencies.Add(propertyReferenceOperation.Property);
                    break;

                case IFieldReferenceOperation fieldReferenceOperation:
                    dependencies.Add(fieldReferenceOperation.Field);
                    break;

                case IEventReferenceOperation eventReferenceOperation:
                    dependencies.Add(eventReferenceOperation.Event);
                    break;

                case IMethodReferenceOperation methodReferenceOperation:
                    dependencies.Add(methodReferenceOperation.Method);
                    break;
            }
        }
    }

    private static void AddTypeSymbol(ITypeSymbol? symbol, ISet<ISymbol> dependencies)
    {
        if (symbol is null)
        {
            return;
        }

        if (!dependencies.Add(symbol))
        {
            return;
        }

        switch (symbol)
        {
            case IArrayTypeSymbol arrayType:
                AddTypeSymbol(arrayType.ElementType, dependencies);
                break;

            case IPointerTypeSymbol pointerType:
                AddTypeSymbol(pointerType.PointedAtType, dependencies);
                break;

            case IFunctionPointerTypeSymbol functionPointerType:
                AddTypeSymbol(functionPointerType.Signature.ReturnType, dependencies);
                foreach (var parameter in functionPointerType.Signature.Parameters)
                {
                    AddTypeSymbol(parameter.Type, dependencies);
                }

                break;

            case INamedTypeSymbol namedType:
                foreach (var typeArgument in namedType.TypeArguments)
                {
                    AddTypeSymbol(typeArgument, dependencies);
                }

                break;
        }
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
}
