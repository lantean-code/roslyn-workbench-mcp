using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal static class DependencyAnalysisHelpers
{
    public static bool IsSupportedCycleGranularity(string value)
    {
        return value is "Project" or "Namespace" or "Type";
    }

    public static bool IsSupportedGraphGranularity(string value)
    {
        return value is "Project" or "Namespace" or "Type" or "Symbol";
    }

    public static async ValueTask<IReadOnlyList<DependencyCycle>> FindCyclesAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var graph = await BuildSourceGraphAsync(granularity, projects, documents, context, cancellationToken).ConfigureAwait(false);
        var adjacency = graph.Edges
            .GroupBy(static edge => edge.FromId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static edge => edge.ToId).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        var nodeLookup = graph.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var indexByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinkByNodeId = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var cycles = new List<DependencyCycle>();
        var index = 0;

        foreach (var node in graph.Nodes.OrderBy(static node => node.DisplayName, StringComparer.Ordinal))
        {
            if (!indexByNodeId.ContainsKey(node.Id))
            {
                Visit(node.Id);
            }
        }

        return cycles
            .OrderBy(static cycle => cycle.Nodes.Count)
            .ThenBy(static cycle => cycle.Nodes[0].DisplayName, StringComparer.Ordinal)
            .ToArray();

        void Visit(string nodeId)
        {
            indexByNodeId[nodeId] = index;
            lowLinkByNodeId[nodeId] = index;
            index++;
            stack.Push(nodeId);
            onStack.Add(nodeId);

            foreach (var nextNodeId in adjacency.GetValueOrDefault(nodeId, []))
            {
                if (!indexByNodeId.ContainsKey(nextNodeId))
                {
                    Visit(nextNodeId);
                    lowLinkByNodeId[nodeId] = Math.Min(lowLinkByNodeId[nodeId], lowLinkByNodeId[nextNodeId]);
                }
                else if (onStack.Contains(nextNodeId))
                {
                    lowLinkByNodeId[nodeId] = Math.Min(lowLinkByNodeId[nodeId], indexByNodeId[nextNodeId]);
                }
            }

            if (lowLinkByNodeId[nodeId] != indexByNodeId[nodeId])
            {
                return;
            }

            var component = new List<GraphNode>();
            string currentNodeId;
            do
            {
                currentNodeId = stack.Pop();
                onStack.Remove(currentNodeId);
                component.Add(nodeLookup[currentNodeId]);
            }
            while (!string.Equals(currentNodeId, nodeId, StringComparison.Ordinal));

            var isSelfCycle = adjacency.GetValueOrDefault(nodeId, []).Contains(nodeId, StringComparer.Ordinal);
            if (component.Count > 1 || isSelfCycle)
            {
                cycles.Add(new DependencyCycle
                {
                    Nodes = component
                        .OrderBy(static graphNode => graphNode.DisplayName, StringComparer.Ordinal)
                        .ToArray(),
                });
            }
        }
    }

    public static async ValueTask<IReadOnlyList<TestImpactInfo>> FindTestImpactsAsync(
        ISymbol targetSymbol,
        IReadOnlyList<Document> documents,
        bool includeReasons,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var impacts = new List<TestImpactInfo>();
        var normalizedTarget = NormalizeSymbol(targetSymbol);
        var normalizedTargetType = GetOwningTypeSymbol(targetSymbol);

        foreach (var document in documents.OrderBy(static item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var methodDeclaration in syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken) is not IMethodSymbol methodSymbol
                    || methodSymbol.IsImplicitlyDeclared
                    || !IsTestMethodCandidate(methodSymbol))
                {
                    continue;
                }

                var dependencies = await CollectSymbolDependenciesAsync(methodSymbol, context.CurrentSolution, cancellationToken).ConfigureAwait(false);
                var hasDirectImpact = dependencies.Any(dependency =>
                    SymbolsMatch(dependency, normalizedTarget)
                    || (normalizedTargetType is not null && SymbolsMatch(GetOwningTypeSymbol(dependency), normalizedTargetType)));

                if (!hasDirectImpact)
                {
                    continue;
                }

                var sourceLocation = methodSymbol.Locations.FirstOrDefault(static location => location.IsInSource);

                impacts.Add(new TestImpactInfo
                {
                    Test = context.Resolver.CreateSymbolReference(methodSymbol),
                    Location = sourceLocation is null ? null : context.Resolver.CreateResolvedLocation(sourceLocation),
                    Reasons = includeReasons
                        ? ["Direct reference to the target symbol or its owning type."]
                        : [],
                });
            }
        }

        return impacts
            .OrderBy(static impact => impact.Test!.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public static async ValueTask<(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)> BuildGraphAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var graph = await BuildSourceGraphAsync(granularity, projects, documents, context, cancellationToken).ConfigureAwait(false);
        return (graph.Nodes, graph.Edges);
    }

    private static async ValueTask<IReadOnlyCollection<ISymbol>> CollectSymbolDependenciesAsync(ISymbol symbol, Solution solution, CancellationToken cancellationToken)
    {
        var dependencies = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        AddSignatureDependencies(symbol, dependencies);

        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntax = await syntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
            if (solution.GetDocument(syntax.SyntaxTree) is not { } document)
            {
                continue;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
            {
                continue;
            }

            AddOperationDependencies(semanticModel, syntax, dependencies, cancellationToken);
        }

        dependencies.RemoveWhere(dependency => SymbolsMatch(dependency, symbol));
        return dependencies;
    }

    private static async ValueTask<IReadOnlyCollection<INamedTypeSymbol>> CollectTypeDependenciesAsync(INamedTypeSymbol symbol, Solution solution, CancellationToken cancellationToken)
    {
        var dependencies = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var dependency in await CollectSymbolDependenciesAsync(symbol, solution, cancellationToken).ConfigureAwait(false))
        {
            AddOwningType(dependency, dependencies);
        }

        foreach (var member in symbol.GetMembers().Where(static member => !member.IsImplicitlyDeclared))
        {
            foreach (var dependency in await CollectSymbolDependenciesAsync(member, solution, cancellationToken).ConfigureAwait(false))
            {
                AddOwningType(dependency, dependencies);
            }
        }

        dependencies.RemoveWhere(dependency => SymbolsMatch(dependency, symbol));
        return dependencies;
    }

    private static async ValueTask<SourceGraph> BuildSourceGraphAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        return granularity switch
        {
            "Project" => BuildProjectGraph(projects),
            "Namespace" => await BuildNamespaceGraphAsync(documents, context, cancellationToken).ConfigureAwait(false),
            "Type" => await BuildTypeGraphAsync(documents, context, cancellationToken).ConfigureAwait(false),
            "Symbol" => await BuildSymbolGraphAsync(documents, context, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unsupported dependency graph granularity."),
        };
    }

    private static SourceGraph BuildProjectGraph(IReadOnlyList<Project> projects)
    {
        var nodes = projects
            .OrderBy(static project => project.Name, StringComparer.Ordinal)
            .Select(static project => new GraphNode
            {
                Id = CreateProjectId(project),
                Kind = "Project",
                DisplayName = project.Name,
            })
            .ToArray();
        var nodeIds = nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = projects
            .OrderBy(static project => project.Name, StringComparer.Ordinal)
            .SelectMany(project => project.ProjectReferences
                .Select(reference => project.Solution.GetProject(reference.ProjectId))
                .Where(static referencedProject => referencedProject is not null)
                .Select(referencedProject => new GraphEdge
                {
                    FromId = CreateProjectId(project),
                    FromDisplayName = project.Name,
                    ToId = CreateProjectId(referencedProject!),
                    ToDisplayName = referencedProject!.Name,
                    Kind = "Dependency",
                }))
            .Where(edge => nodeIds.Contains(edge.ToId))
            .DistinctBy(static edge => (edge.FromId, edge.ToId, edge.Kind))
            .OrderBy(static edge => edge.FromDisplayName, StringComparer.Ordinal)
            .ThenBy(static edge => edge.ToDisplayName, StringComparer.Ordinal)
            .ToArray();

        return new SourceGraph(nodes, edges);
    }

    private static async ValueTask<SourceGraph> BuildNamespaceGraphAsync(IReadOnlyList<Document> documents, IQueryContext context, CancellationToken cancellationToken)
    {
        var sourceTypes = await GetSourceTypesAsync(documents, cancellationToken).ConfigureAwait(false);
        var namespaces = sourceTypes
            .Select(GetNamespaceName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        var nodes = namespaces
            .Select(static name => new GraphNode
            {
                Id = CreateNamespaceId(name),
                Kind = "Namespace",
                DisplayName = name,
            })
            .ToArray();
        var namespaceIds = nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = new HashSet<(string FromId, string ToId)>();

        foreach (var sourceType in sourceTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fromNamespace = GetNamespaceName(sourceType);
            foreach (var dependency in await CollectTypeDependenciesAsync(sourceType, context.CurrentSolution, cancellationToken).ConfigureAwait(false))
            {
                var toNamespace = GetNamespaceName(dependency);
                var edge = (CreateNamespaceId(fromNamespace), CreateNamespaceId(toNamespace));
                if (namespaceIds.Contains(edge.Item2))
                {
                    edges.Add(edge);
                }
            }
        }

        return new SourceGraph(
            nodes,
            edges
                .Select(edge => new GraphEdge
                {
                    FromId = edge.FromId,
                    FromDisplayName = edge.FromId["namespace:".Length..],
                    ToId = edge.ToId,
                    ToDisplayName = edge.ToId["namespace:".Length..],
                    Kind = "Dependency",
                })
                .OrderBy(static edge => edge.FromDisplayName, StringComparer.Ordinal)
                .ThenBy(static edge => edge.ToDisplayName, StringComparer.Ordinal)
                .ToArray());
    }

    private static async ValueTask<SourceGraph> BuildTypeGraphAsync(IReadOnlyList<Document> documents, IQueryContext context, CancellationToken cancellationToken)
    {
        var sourceTypes = await GetSourceTypesAsync(documents, cancellationToken).ConfigureAwait(false);
        var typeNodes = new Dictionary<INamedTypeSymbol, GraphNode>(SymbolEqualityComparer.Default);
        foreach (var sourceType in sourceTypes)
        {
            typeNodes[sourceType] = CreateSymbolGraphNode(sourceType, "Type", context);
        }

        var typeNamesById = typeNodes.Values.ToDictionary(static node => node.Id, static node => node.DisplayName, StringComparer.Ordinal);
        var edges = new HashSet<(string FromId, string ToId)>();

        foreach (var sourceType in sourceTypes.OrderBy(static item => item.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var dependency in await CollectTypeDependenciesAsync(sourceType, context.CurrentSolution, cancellationToken).ConfigureAwait(false))
            {
                if (typeNodes.TryGetValue(NormalizeNamedTypeSymbol(dependency), out var dependencyNode))
                {
                    edges.Add((typeNodes[sourceType].Id, dependencyNode.Id));
                }
            }
        }

        return new SourceGraph(
            typeNodes.Values.OrderBy(static node => node.DisplayName, StringComparer.Ordinal).ToArray(),
            edges
                .Select(edge => new GraphEdge
                {
                    FromId = edge.FromId,
                    FromDisplayName = typeNamesById[edge.FromId],
                    ToId = edge.ToId,
                    ToDisplayName = typeNamesById[edge.ToId],
                    Kind = "Dependency",
                })
                .OrderBy(static edge => edge.FromDisplayName, StringComparer.Ordinal)
                .ThenBy(static edge => edge.ToDisplayName, StringComparer.Ordinal)
                .ToArray());
    }

    private static async ValueTask<SourceGraph> BuildSymbolGraphAsync(IReadOnlyList<Document> documents, IQueryContext context, CancellationToken cancellationToken)
    {
        var sourceSymbols = await GetSourceSymbolsAsync(documents, cancellationToken).ConfigureAwait(false);
        var symbolNodes = new Dictionary<ISymbol, GraphNode>(SymbolEqualityComparer.Default);
        foreach (var sourceSymbol in sourceSymbols)
        {
            symbolNodes[sourceSymbol] = CreateSymbolGraphNode(sourceSymbol, "Symbol", context);
        }

        var symbolNamesById = symbolNodes.Values.ToDictionary(static node => node.Id, static node => node.DisplayName, StringComparer.Ordinal);
        var edges = new HashSet<(string FromId, string ToId)>();

        foreach (var sourceSymbol in sourceSymbols.OrderBy(static item => item.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var dependency in await CollectSymbolDependenciesAsync(sourceSymbol, context.CurrentSolution, cancellationToken).ConfigureAwait(false))
            {
                var normalizedDependency = NormalizeSymbol(dependency);
                if (symbolNodes.TryGetValue(normalizedDependency, out var dependencyNode))
                {
                    edges.Add((symbolNodes[sourceSymbol].Id, dependencyNode.Id));
                }
            }
        }

        return new SourceGraph(
            symbolNodes.Values.OrderBy(static node => node.DisplayName, StringComparer.Ordinal).ToArray(),
            edges
                .Select(edge => new GraphEdge
                {
                    FromId = edge.FromId,
                    FromDisplayName = symbolNamesById[edge.FromId],
                    ToId = edge.ToId,
                    ToDisplayName = symbolNamesById[edge.ToId],
                    Kind = "Dependency",
                })
                .OrderBy(static edge => edge.FromDisplayName, StringComparer.Ordinal)
                .ThenBy(static edge => edge.ToDisplayName, StringComparer.Ordinal)
                .ToArray());
    }

    private static Document[] GetOrderedDocuments(IReadOnlyList<Document> documents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return documents
            .OrderBy(static document => document.FilePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static async ValueTask<INamedTypeSymbol[]> GetSourceTypesAsync(IReadOnlyList<Document> documents, CancellationToken cancellationToken)
    {
        var orderedDocuments = GetOrderedDocuments(documents, cancellationToken);
        var symbols = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var document in orderedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var declaration in syntaxRoot.DescendantNodes().Where(IsTypeDeclarationNode))
            {
                if (GetDeclaredSymbol(semanticModel, declaration, cancellationToken) is INamedTypeSymbol symbol && !symbol.IsImplicitlyDeclared)
                {
                    symbols.Add(NormalizeNamedTypeSymbol(symbol));
                }
            }
        }

        return symbols
            .OrderBy(static symbol => symbol.ToDisplayString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static async ValueTask<ISymbol[]> GetSourceSymbolsAsync(IReadOnlyList<Document> documents, CancellationToken cancellationToken)
    {
        var orderedDocuments = GetOrderedDocuments(documents, cancellationToken);
        var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var document in orderedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var declaration in syntaxRoot.DescendantNodes().Where(IsSymbolDeclarationNode))
            {
                if (GetDeclaredSymbol(semanticModel, declaration, cancellationToken) is { } symbol && !symbol.IsImplicitlyDeclared)
                {
                    symbols.Add(NormalizeSymbol(symbol));
                }
            }
        }

        return symbols
            .OrderBy(static symbol => symbol.ToDisplayString(), StringComparer.Ordinal)
            .ToArray();
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

    private static void AddOperationDependencies(SemanticModel semanticModel, SyntaxNode syntax, ISet<ISymbol> dependencies, CancellationToken cancellationToken)
    {
        var executableNode = GetExecutableNode(syntax);
        var rootOperation = executableNode is null
            ? semanticModel.GetOperation(syntax, cancellationToken)
            : semanticModel.GetOperation(executableNode, cancellationToken);
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

    private static GraphNode CreateSymbolGraphNode(ISymbol symbol, string kind, IQueryContext context)
    {
        return new GraphNode
        {
            Id = CreateSymbolId(symbol),
            Kind = kind,
            DisplayName = context.Resolver.CreateSymbolReference(symbol).DisplayName,
            Symbol = context.Resolver.CreateSymbolReference(symbol),
        };
    }

    private static string CreateProjectId(Project project)
    {
        return $"project:{project.FilePath ?? project.Name}";
    }

    private static string CreateNamespaceId(string name)
    {
        return $"namespace:{name}";
    }

    private static string CreateSymbolId(ISymbol symbol)
    {
        var documentationCommentId = symbol.GetDocumentationCommentId();
        if (!string.IsNullOrWhiteSpace(documentationCommentId))
        {
            return documentationCommentId;
        }

        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is not null)
        {
            return $"{symbol.Kind}:{sourceLocation.SourceTree?.FilePath}:{sourceLocation.SourceSpan.Start}:{symbol.Name}";
        }

        return $"{symbol.Kind}:{symbol.ToDisplayString()}";
    }

    private static ISymbol? GetDeclaredSymbol(SemanticModel semanticModel, SyntaxNode declaration, CancellationToken cancellationToken)
    {
        return declaration switch
        {
            BaseTypeDeclarationSyntax typeDeclarationSyntax => semanticModel.GetDeclaredSymbol(typeDeclarationSyntax, cancellationToken),
            DelegateDeclarationSyntax delegateDeclarationSyntax => semanticModel.GetDeclaredSymbol(delegateDeclarationSyntax, cancellationToken),
            BaseMethodDeclarationSyntax methodDeclarationSyntax => semanticModel.GetDeclaredSymbol(methodDeclarationSyntax, cancellationToken),
            PropertyDeclarationSyntax propertyDeclarationSyntax => semanticModel.GetDeclaredSymbol(propertyDeclarationSyntax, cancellationToken),
            EventDeclarationSyntax eventDeclarationSyntax => semanticModel.GetDeclaredSymbol(eventDeclarationSyntax, cancellationToken),
            VariableDeclaratorSyntax variableDeclaratorSyntax when variableDeclaratorSyntax.Parent?.Parent is FieldDeclarationSyntax => semanticModel.GetDeclaredSymbol(variableDeclaratorSyntax, cancellationToken),
            _ => null,
        };
    }

    private static string GetNamespaceName(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace?.ToDisplayString();
        return string.IsNullOrWhiteSpace(namespaceName) ? "<global namespace>" : namespaceName;
    }

    private static SyntaxNode? GetExecutableNode(SyntaxNode node)
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

    private static INamedTypeSymbol? GetOwningTypeSymbol(ISymbol? symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedTypeSymbol => NormalizeNamedTypeSymbol(namedTypeSymbol),
            _ => NormalizeNamedTypeSymbol(symbol?.ContainingType),
        };
    }

    private static bool IsSymbolDeclarationNode(SyntaxNode node)
    {
        return node is BaseTypeDeclarationSyntax
            or DelegateDeclarationSyntax
            or BaseMethodDeclarationSyntax
            or PropertyDeclarationSyntax
            or EventDeclarationSyntax
            or VariableDeclaratorSyntax;
    }

    private static bool IsTestMethodCandidate(IMethodSymbol symbol)
    {
        var containingTypeName = symbol.ContainingType?.Name ?? string.Empty;
        return containingTypeName.EndsWith("Tests", StringComparison.Ordinal)
            || containingTypeName.EndsWith("Test", StringComparison.Ordinal)
            || containingTypeName.EndsWith("Specs", StringComparison.Ordinal)
            || containingTypeName.EndsWith("Spec", StringComparison.Ordinal)
            || symbol.Name.StartsWith("GIVEN_", StringComparison.Ordinal)
            || symbol.Name.Contains("_THEN_Should", StringComparison.Ordinal);
    }

    private static bool IsTypeDeclarationNode(SyntaxNode node)
    {
        return node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;
    }

    private static void AddOwningType(ISymbol dependency, ISet<INamedTypeSymbol> dependencies)
    {
        var owningType = GetOwningTypeSymbol(dependency);
        if (owningType is not null)
        {
            dependencies.Add(owningType);
        }
    }

    private static void AddTypeSymbol(ITypeSymbol? symbol, ISet<ISymbol> dependencies)
    {
        if (symbol is not null)
        {
            dependencies.Add(symbol);
        }
    }

    private static INamedTypeSymbol NormalizeNamedTypeSymbol(INamedTypeSymbol? symbol)
    {
        return symbol?.OriginalDefinition ?? symbol!;
    }

    private static ISymbol NormalizeSymbol(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedTypeSymbol => namedTypeSymbol.OriginalDefinition,
            IMethodSymbol methodSymbol => methodSymbol.OriginalDefinition,
            IPropertySymbol propertySymbol => propertySymbol.OriginalDefinition,
            IEventSymbol eventSymbol => eventSymbol.OriginalDefinition,
            _ => symbol,
        };
    }

    private static bool SymbolsMatch(ISymbol? left, ISymbol? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(NormalizeSymbol(left), NormalizeSymbol(right));
    }

    private sealed record SourceGraph(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges);
}
