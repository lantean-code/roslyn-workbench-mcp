namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-symbol-dependencies", "Get Symbol Dependencies", "Returns the direct symbols used by a resolved symbol.")]
internal sealed class GetSymbolDependenciesTool : QueryToolHandler<GetSymbolDependenciesRequest, SymbolDependenciesData>
{
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

        var orderedDependencies = dependencies
            .OrderBy(item => context.WorkspaceResolver.CreateSymbolReference(item).DisplayName, StringComparer.Ordinal)
            .Select(item => new DependencyInfo
            {
                Symbol = context.WorkspaceResolver.CreateSymbolReference(item),
                Kind = item.Kind.ToString(),
                AssemblyName = request.IncludeAssemblies ? item.ContainingAssembly?.Name : null,
            })
            .ToArray();

        return PluginExecutionResult<SymbolDependenciesData>.Success(new SymbolDependenciesData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Dependencies = ToolExecutionHelpers.CreateBoundedCollection(
                orderedDependencies,
                request.EffectiveDependenciesLimit),
        });
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
        var rootOperation = executableNode is null ? semanticModel.GetOperation(syntax, cancellationToken) : semanticModel.GetOperation(executableNode, cancellationToken);
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

        dependencies.Add(symbol);
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
