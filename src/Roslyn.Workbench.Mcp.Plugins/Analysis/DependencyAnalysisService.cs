namespace Roslyn.Workbench.Mcp.Plugins.Analysis;

internal sealed class DependencyAnalysisService : IDependencyAnalysisService
{
    public bool IsSupportedCycleGranularity(string value)
    {
        return value is "Project" or "Namespace" or "Type";
    }

    public bool IsSupportedGraphGranularity(string value)
    {
        return value is "Project" or "Namespace" or "Type" or "Symbol";
    }

    public async ValueTask<(IReadOnlyList<DependencyCycle> Cycles, int TotalCount)> FindCyclesAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        int maxResults,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var analysisState = new DependencyAnalysisState(context.CurrentSolution);
        var graph = await BuildSourceGraphAsync(granularity, projects, documents, null, null, analysisState, context, cancellationToken);
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

        var orderedCycles = cycles
            .OrderBy(static cycle => cycle.Nodes.Count)
            .ThenBy(static cycle => cycle.Nodes[0].DisplayName, StringComparer.Ordinal)
            .ToArray();

        var hasMore = orderedCycles.Length > maxResults;
        var selectedCycles = hasMore
            ? orderedCycles.Take(maxResults).ToArray()
            : orderedCycles;

        return (selectedCycles, orderedCycles.Length);

        void Visit(string nodeId)
        {
            indexByNodeId[nodeId] = index;
            lowLinkByNodeId[nodeId] = index;
            index++;
            stack.Push(nodeId);
            onStack.Add(nodeId);

            foreach (var nextNodeId in adjacency.GetValueOrDefault(nodeId, []))
            {
                if (!indexByNodeId.TryGetValue(nextNodeId, out var value))
                {
                    Visit(nextNodeId);
                    lowLinkByNodeId[nodeId] = Math.Min(lowLinkByNodeId[nodeId], lowLinkByNodeId[nextNodeId]);
                }
                else if (onStack.Contains(nextNodeId))
                {
                    lowLinkByNodeId[nodeId] = Math.Min(lowLinkByNodeId[nodeId], value);
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

    public async ValueTask<(IReadOnlyList<TestImpactInfo> Tests, bool HasMore)> FindTestImpactsAsync(
        ISymbol targetSymbol,
        IReadOnlyList<Document> documents,
        bool includeReasons,
        int maxResults,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var analysisState = new DependencyAnalysisState(context.CurrentSolution);
        var normalizedTarget = NormalizeSymbol(targetSymbol);
        var normalizedTargetType = GetOwningTypeSymbol(targetSymbol);
        var testMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

        foreach (var document in documents.OrderBy(static item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await analysisState.GetSemanticModelAsync(document, cancellationToken);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var syntaxNode in syntaxRoot.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (syntaxNode is not MethodDeclarationSyntax methodDeclaration)
                {
                    continue;
                }

                if (semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken) is not IMethodSymbol methodSymbol
                    || methodSymbol.IsImplicitlyDeclared
                    || !IsTestMethodCandidate(methodSymbol))
                {
                    continue;
                }

                testMethods.Add(methodSymbol);
            }
        }

        var orderedTestMethods = testMethods
            .OrderBy(static method => method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal)
            .ToArray();

        var impacts = new List<TestImpactInfo>();

        foreach (var methodSymbol in orderedTestMethods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasDirectImpact = await HasTargetDependencyAsync(
                methodSymbol,
                normalizedTarget,
                normalizedTargetType,
                analysisState,
                cancellationToken);

            if (!hasDirectImpact)
            {
                continue;
            }

            if (impacts.Count == maxResults)
            {
                return (impacts, true);
            }

            var sourceLocation = methodSymbol.Locations.FirstOrDefault(static location => location.IsInSource);
            impacts.Add(new TestImpactInfo
            {
                Test = context.WorkspaceResolver.CreateSymbolReference(methodSymbol),
                Location = sourceLocation is null ? null : context.WorkspaceResolver.CreateResolvedLocation(sourceLocation),
                Reasons = includeReasons
                    ? ["Direct reference to the target symbol or its owning type."]
                    : null,
            });
        }

        return (impacts, false);
    }

    public async ValueTask<(IReadOnlyList<GraphNode> Nodes, bool NodesHaveMore, IReadOnlyList<GraphEdge> Edges, bool EdgesHaveMore)> BuildGraphAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        int maxNodes,
        int maxEdges,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var analysisState = new DependencyAnalysisState(context.CurrentSolution);
        var graph = await BuildSourceGraphAsync(granularity, projects, documents, maxNodes, maxEdges, analysisState, context, cancellationToken);

        return (graph.Nodes, graph.NodesHaveMore, graph.Edges, graph.EdgesHaveMore);
    }

    private static async ValueTask<IReadOnlyCollection<ISymbol>> CollectSymbolDependenciesAsync(ISymbol symbol, DependencyAnalysisState analysisState, CancellationToken cancellationToken)
    {
        var dependencies = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        AddSignatureDependencies(symbol, dependencies);

        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntax = await syntaxReference.GetSyntaxAsync(cancellationToken);
            var semanticModel = await analysisState.GetSemanticModelAsync(syntax.SyntaxTree, cancellationToken);
            if (semanticModel is null)
            {
                continue;
            }

            AddOperationDependencies(semanticModel, syntax, dependencies, cancellationToken);
        }

        dependencies.RemoveWhere(dependency => SymbolsMatch(dependency, symbol));
        return dependencies;
    }

    private static async ValueTask<IReadOnlyCollection<INamedTypeSymbol>> CollectTypeDependenciesAsync(INamedTypeSymbol symbol, DependencyAnalysisState analysisState, CancellationToken cancellationToken)
    {
        var dependencies = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var dependency in await CollectSymbolDependenciesAsync(symbol, analysisState, cancellationToken))
        {
            AddOwningType(dependency, dependencies);
        }

        foreach (var member in symbol.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
            {
                continue;
            }

            foreach (var dependency in await CollectSymbolDependenciesAsync(member, analysisState, cancellationToken))
            {
                AddOwningType(dependency, dependencies);
            }
        }

        dependencies.RemoveWhere(dependency => SymbolsMatch(dependency, symbol));
        return dependencies;
    }

    private static async ValueTask<bool> HasTargetDependencyAsync(
        IMethodSymbol sourceSymbol,
        ISymbol targetSymbol,
        INamedTypeSymbol? targetType,
        DependencyAnalysisState analysisState,
        CancellationToken cancellationToken)
    {
        if (IsTargetDependency(sourceSymbol.ReturnType, sourceSymbol, targetSymbol, targetType))
        {
            return true;
        }

        foreach (var parameter in sourceSymbol.Parameters)
        {
            if (IsTargetDependency(parameter.Type, sourceSymbol, targetSymbol, targetType))
            {
                return true;
            }
        }

        foreach (var syntaxReference in sourceSymbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntax = await syntaxReference.GetSyntaxAsync(cancellationToken);
            var semanticModel = await analysisState.GetSemanticModelAsync(syntax.SyntaxTree, cancellationToken);
            if (semanticModel is null)
            {
                continue;
            }

            var rootOperation = GetRootOperation(semanticModel, syntax, cancellationToken);

            if (rootOperation is null)
            {
                continue;
            }

            foreach (var operation in rootOperation.DescendantsAndSelf())
            {
                if (IsTargetDependency(operation.Type, sourceSymbol, targetSymbol, targetType)
                    || GetReferencedSymbol(operation) is { } referencedSymbol
                        && IsTargetDependency(referencedSymbol, sourceSymbol, targetSymbol, targetType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTargetDependency(ISymbol? dependency, ISymbol sourceSymbol, ISymbol targetSymbol, INamedTypeSymbol? targetType)
    {
        if (dependency is null || SymbolsMatch(dependency, sourceSymbol))
        {
            return false;
        }

        return SymbolsMatch(dependency, targetSymbol)
            || targetType is not null && SymbolsMatch(GetOwningTypeSymbol(dependency), targetType);
    }

    private static ISymbol? GetReferencedSymbol(IOperation operation)
    {
        return operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation objectCreation => objectCreation.Constructor,
            IPropertyReferenceOperation propertyReference => propertyReference.Property,
            IFieldReferenceOperation fieldReference => fieldReference.Field,
            IEventReferenceOperation eventReference => eventReference.Event,
            IMethodReferenceOperation methodReference => methodReference.Method,
            _ => null,
        };
    }

    private static async ValueTask<SourceGraph> BuildSourceGraphAsync(
        string granularity,
        IReadOnlyList<Project> projects,
        IReadOnlyList<Document> documents,
        int? maxNodes,
        int? maxEdges,
        DependencyAnalysisState analysisState,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        return granularity switch
        {
            "Project" => BuildProjectGraph(projects, maxNodes, maxEdges),
            "Namespace" => await BuildNamespaceGraphAsync(documents, maxNodes, maxEdges, analysisState, cancellationToken),
            "Type" => await BuildTypeGraphAsync(documents, maxNodes, maxEdges, analysisState, context, cancellationToken),
            "Symbol" => await BuildSymbolGraphAsync(documents, maxNodes, maxEdges, analysisState, context, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported dependency graph granularity."),
        };
    }

    private static SourceGraph BuildProjectGraph(IReadOnlyList<Project> projects, int? maxNodes, int? maxEdges)
    {
        var orderedProjects = projects
            .OrderBy(static project => project.Name, StringComparer.Ordinal)
            .ToArray();

        var selectedProjects = ApplyOptionalLimit(orderedProjects, maxNodes, out var nodesHaveMore);
        var nodes = selectedProjects
            .Select(static project => new GraphNode
            {
                Id = CreateProjectId(project),
                Kind = "Project",
                DisplayName = project.Name,
            })
            .ToArray();

        var nodeIds = nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = new List<GraphEdge>();
        var edgeKeys = new HashSet<(string FromId, string ToId)>();
        var edgesHaveMore = false;

        foreach (var project in selectedProjects)
        {
            var referencedProjects = new List<Project>();
            foreach (var reference in project.ProjectReferences)
            {
                var referencedProject = project.Solution.GetProject(reference.ProjectId);
                if (referencedProject is null
                    || !nodeIds.Contains(CreateProjectId(referencedProject)))
                {
                    continue;
                }

                referencedProjects.Add(referencedProject);
            }

            foreach (var referencedProject in referencedProjects.OrderBy(
                static referencedProject => referencedProject.Name,
                StringComparer.Ordinal))
            {
                var edgeKey = (CreateProjectId(project), CreateProjectId(referencedProject));
                if (!edgeKeys.Add(edgeKey))
                {
                    continue;
                }

                if (HasReachedLimit(edges.Count, maxEdges))
                {
                    edgesHaveMore = true;
                    break;
                }

                edges.Add(CreateGraphEdge(edgeKey.Item1, project.Name, edgeKey.Item2, referencedProject.Name));
            }

            if (edgesHaveMore)
            {
                break;
            }
        }

        return new SourceGraph(nodes, nodesHaveMore, edges, edgesHaveMore);
    }

    private static async ValueTask<SourceGraph> BuildNamespaceGraphAsync(
        IReadOnlyList<Document> documents,
        int? maxNodes,
        int? maxEdges,
        DependencyAnalysisState analysisState,
        CancellationToken cancellationToken)
    {
        var sourceTypes = await GetSourceTypesAsync(documents, analysisState, cancellationToken);
        var namespaceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sourceType in sourceTypes)
        {
            namespaceNames.Add(GetNamespaceName(sourceType));
        }

        var namespaces = namespaceNames
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();

        var selectedNamespaces = ApplyOptionalLimit(namespaces, maxNodes, out var nodesHaveMore);
        var nodes = selectedNamespaces
            .Select(static name => new GraphNode
            {
                Id = CreateNamespaceId(name),
                Kind = "Namespace",
                DisplayName = name,
            })
            .ToArray();

        var selectedNamespaceNames = selectedNamespaces.ToHashSet(StringComparer.Ordinal);
        var sourceTypesByNamespace = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
        foreach (var sourceType in sourceTypes)
        {
            var namespaceName = GetNamespaceName(sourceType);
            if (!sourceTypesByNamespace.TryGetValue(namespaceName, out var namespaceTypes))
            {
                namespaceTypes = [];
                sourceTypesByNamespace.Add(namespaceName, namespaceTypes);
            }

            namespaceTypes.Add(sourceType);
        }

        var edges = new List<GraphEdge>();
        var edgesHaveMore = false;

        foreach (var fromNamespace in selectedNamespaces)
        {
            var targetNamespaces = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sourceType in sourceTypesByNamespace[fromNamespace])
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var dependency in await CollectTypeDependenciesAsync(sourceType, analysisState, cancellationToken))
                {
                    var targetNamespace = GetNamespaceName(dependency);
                    if (selectedNamespaceNames.Contains(targetNamespace))
                    {
                        targetNamespaces.Add(targetNamespace);
                    }
                }
            }

            foreach (var targetNamespace in targetNamespaces.Order(StringComparer.Ordinal))
            {
                if (HasReachedLimit(edges.Count, maxEdges))
                {
                    edgesHaveMore = true;
                    break;
                }

                edges.Add(CreateGraphEdge(
                    CreateNamespaceId(fromNamespace),
                    fromNamespace,
                    CreateNamespaceId(targetNamespace),
                    targetNamespace));
            }

            if (edgesHaveMore)
            {
                break;
            }
        }

        return new SourceGraph(nodes, nodesHaveMore, edges, edgesHaveMore);
    }

    private static async ValueTask<SourceGraph> BuildTypeGraphAsync(
        IReadOnlyList<Document> documents,
        int? maxNodes,
        int? maxEdges,
        DependencyAnalysisState analysisState,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var sourceTypes = await GetSourceTypesAsync(documents, analysisState, cancellationToken);
        var orderedTypes = sourceTypes
            .OrderBy(static symbol => GetGraphDisplayName(symbol), StringComparer.Ordinal)
            .ToArray();

        var selectedTypes = ApplyOptionalLimit(orderedTypes, maxNodes, out var nodesHaveMore);
        var typeNodes = new Dictionary<INamedTypeSymbol, GraphNode>(SymbolEqualityComparer.Default);
        foreach (var sourceType in selectedTypes)
        {
            typeNodes[sourceType] = CreateSymbolGraphNode(sourceType, "Type", context);
        }

        var edges = await BuildSymbolEdgesAsync(selectedTypes, typeNodes, maxEdges, analysisState, cancellationToken);

        var nodes = selectedTypes.Select(type => typeNodes[type]).ToArray();

        return new SourceGraph(nodes, nodesHaveMore, edges.Edges, edges.HasMore);
    }

    private static async ValueTask<SourceGraph> BuildSymbolGraphAsync(
        IReadOnlyList<Document> documents,
        int? maxNodes,
        int? maxEdges,
        DependencyAnalysisState analysisState,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var sourceSymbols = await GetSourceSymbolsAsync(documents, analysisState, cancellationToken);
        var orderedSymbols = sourceSymbols
            .OrderBy(static symbol => GetGraphDisplayName(symbol), StringComparer.Ordinal)
            .ToArray();

        var selectedSymbols = ApplyOptionalLimit(orderedSymbols, maxNodes, out var nodesHaveMore);
        var symbolNodes = new Dictionary<ISymbol, GraphNode>(SymbolEqualityComparer.Default);
        foreach (var sourceSymbol in selectedSymbols)
        {
            symbolNodes[sourceSymbol] = CreateSymbolGraphNode(sourceSymbol, "Symbol", context);
        }

        var edges = await BuildSymbolEdgesAsync(selectedSymbols, symbolNodes, maxEdges, analysisState, cancellationToken);

        var nodes = selectedSymbols.Select(symbol => symbolNodes[symbol]).ToArray();

        return new SourceGraph(nodes, nodesHaveMore, edges.Edges, edges.HasMore);
    }

    private static async ValueTask<(IReadOnlyList<GraphEdge> Edges, bool HasMore)> BuildSymbolEdgesAsync(
        IReadOnlyList<INamedTypeSymbol> sourceTypes,
        Dictionary<INamedTypeSymbol, GraphNode> typeNodes,
        int? maxEdges,
        DependencyAnalysisState analysisState,
        CancellationToken cancellationToken)
    {
        var edges = new List<GraphEdge>();
        foreach (var sourceType in sourceTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetNodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
            foreach (var dependency in await CollectTypeDependenciesAsync(sourceType, analysisState, cancellationToken))
            {
                if (typeNodes.TryGetValue(NormalizeNamedTypeSymbol(dependency), out var targetNode))
                {
                    targetNodes.TryAdd(targetNode.Id, targetNode);
                }
            }

            foreach (var targetNode in targetNodes.Values.OrderBy(static node => node.DisplayName, StringComparer.Ordinal))
            {
                if (HasReachedLimit(edges.Count, maxEdges))
                {
                    return (edges, true);
                }

                var sourceNode = typeNodes[sourceType];
                edges.Add(CreateGraphEdge(sourceNode.Id, sourceNode.DisplayName, targetNode.Id, targetNode.DisplayName));
            }
        }

        return (edges, false);
    }

    private static async ValueTask<(IReadOnlyList<GraphEdge> Edges, bool HasMore)> BuildSymbolEdgesAsync(
        IReadOnlyList<ISymbol> sourceSymbols,
        Dictionary<ISymbol, GraphNode> symbolNodes,
        int? maxEdges,
        DependencyAnalysisState analysisState,
        CancellationToken cancellationToken)
    {
        var edges = new List<GraphEdge>();
        foreach (var sourceSymbol in sourceSymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetNodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
            foreach (var dependency in await CollectSymbolDependenciesAsync(sourceSymbol, analysisState, cancellationToken))
            {
                if (symbolNodes.TryGetValue(NormalizeSymbol(dependency), out var targetNode))
                {
                    targetNodes.TryAdd(targetNode.Id, targetNode);
                }
            }

            foreach (var targetNode in targetNodes.Values.OrderBy(static node => node.DisplayName, StringComparer.Ordinal))
            {
                if (HasReachedLimit(edges.Count, maxEdges))
                {
                    return (edges, true);
                }

                var sourceNode = symbolNodes[sourceSymbol];
                edges.Add(CreateGraphEdge(sourceNode.Id, sourceNode.DisplayName, targetNode.Id, targetNode.DisplayName));
            }
        }

        return (edges, false);
    }

    private static Document[] GetOrderedDocuments(IReadOnlyList<Document> documents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return documents
            .OrderBy(static document => document.FilePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static async ValueTask<INamedTypeSymbol[]> GetSourceTypesAsync(
        IReadOnlyList<Document> documents,
        DependencyAnalysisState analysisState,
        CancellationToken cancellationToken)
    {
        var orderedDocuments = GetOrderedDocuments(documents, cancellationToken);
        var symbols = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var document in orderedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await analysisState.GetSemanticModelAsync(document, cancellationToken);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var declaration in syntaxRoot.DescendantNodes())
            {
                if (!IsTypeDeclarationNode(declaration))
                {
                    continue;
                }

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

    private static async ValueTask<ISymbol[]> GetSourceSymbolsAsync(
        IReadOnlyList<Document> documents,
        DependencyAnalysisState analysisState,
        CancellationToken cancellationToken)
    {
        var orderedDocuments = GetOrderedDocuments(documents, cancellationToken);
        var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var document in orderedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var semanticModel = await analysisState.GetSemanticModelAsync(document, cancellationToken);
            if (syntaxRoot is null || semanticModel is null)
            {
                continue;
            }

            foreach (var declaration in syntaxRoot.DescendantNodes())
            {
                if (!IsSymbolDeclarationNode(declaration))
                {
                    continue;
                }

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

    private static void AddOperationDependencies(SemanticModel semanticModel, SyntaxNode syntax, HashSet<ISymbol> dependencies, CancellationToken cancellationToken)
    {
        var rootOperation = GetRootOperation(semanticModel, syntax, cancellationToken);

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
        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);

        return new GraphNode
        {
            Id = CreateSymbolId(symbol),
            Kind = kind,
            DisplayName = symbolReference.DisplayName,
            Symbol = symbolReference,
        };
    }

    private static GraphEdge CreateGraphEdge(string fromId, string fromDisplayName, string toId, string toDisplayName)
    {
        return new GraphEdge
        {
            FromId = fromId,
            FromDisplayName = fromDisplayName,
            ToId = toId,
            ToDisplayName = toDisplayName,
            Kind = "Dependency",
        };
    }

    private static string GetGraphDisplayName(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    private static IReadOnlyList<T> ApplyOptionalLimit<T>(IReadOnlyList<T> items, int? maxResults, out bool hasMore)
    {
        if (maxResults is null)
        {
            hasMore = false;
            return items;
        }

        hasMore = items.Count > maxResults.Value;
        return hasMore ? items.Take(maxResults.Value).ToArray() : items;
    }

    private static bool HasReachedLimit(int count, int? maxResults)
    {
        return maxResults is not null && count >= maxResults.Value;
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

    private static IOperation? GetRootOperation(
        SemanticModel semanticModel,
        SyntaxNode syntax,
        CancellationToken cancellationToken)
    {
        var executableNode = GetExecutableNode(syntax);
        if (executableNode is not null)
        {
            return semanticModel.GetOperation(executableNode, cancellationToken);
        }

        return semanticModel.GetOperation(syntax, cancellationToken);
    }

    private static INamedTypeSymbol? GetOwningTypeSymbol(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedTypeSymbol => NormalizeNamedTypeSymbol(namedTypeSymbol),
            _ => symbol.ContainingType is { } containingType
                ? NormalizeNamedTypeSymbol(containingType)
                : null,
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

    private static void AddOwningType(ISymbol dependency, HashSet<INamedTypeSymbol> dependencies)
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

    private static INamedTypeSymbol NormalizeNamedTypeSymbol(INamedTypeSymbol symbol)
    {
        return symbol.OriginalDefinition;
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

    private sealed class DependencyAnalysisState
    {
        private readonly Solution _solution;
        private readonly Dictionary<DocumentId, SemanticModel?> _semanticModels = [];

        public DependencyAnalysisState(Solution solution)
        {
            _solution = solution;
        }

        public async ValueTask<SemanticModel?> GetSemanticModelAsync(Document document, CancellationToken cancellationToken)
        {
            if (_semanticModels.TryGetValue(document.Id, out var semanticModel))
            {
                return semanticModel;
            }

            semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            _semanticModels.Add(document.Id, semanticModel);

            return semanticModel;
        }

        public ValueTask<SemanticModel?> GetSemanticModelAsync(SyntaxTree syntaxTree, CancellationToken cancellationToken)
        {
            var document = _solution.GetDocument(syntaxTree);
            if (document is not null)
            {
                return GetSemanticModelAsync(document, cancellationToken);
            }

            return ValueTask.FromResult<SemanticModel?>(null);
        }
    }

    private sealed record SourceGraph
    {
        public IReadOnlyList<GraphNode> Nodes { get; }

        public bool NodesHaveMore { get; }

        public IReadOnlyList<GraphEdge> Edges { get; }

        public bool EdgesHaveMore { get; }

        public SourceGraph(
            IReadOnlyList<GraphNode> nodes,
            bool nodesHaveMore,
            IReadOnlyList<GraphEdge> edges,
            bool edgesHaveMore)
        {
            Nodes = nodes;
            NodesHaveMore = nodesHaveMore;
            Edges = edges;
            EdgesHaveMore = edgesHaveMore;
        }
    }
}
