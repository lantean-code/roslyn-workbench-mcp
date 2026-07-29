using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-type-hierarchy", "Get Type Hierarchy", "Returns base, interface, and optional derived type relationships for a resolved type.")]
internal sealed class GetTypeHierarchyTool : QueryToolHandler<GetTypeHierarchyRequest, TypeHierarchyData>
{
    protected override async ValueTask<PluginExecutionResult<TypeHierarchyData>> ExecuteCoreAsync(GetTypeHierarchyRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<TypeHierarchyData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return PluginExecutionResult.Rejected<TypeHierarchyData>("InvalidRequest", "Get type hierarchy requires a named type symbol.");
        }

        var baseTypes = new List<SymbolReference>();
        var baseTypeDepth = 0;
        var baseTypeCount = 0;
        for (var current = namedType.BaseType; current is not null && baseTypeDepth < request.MaxDepth; current = current.BaseType)
        {
            baseTypeDepth++;
            baseTypeCount++;
            if (baseTypes.Count < request.EffectiveBaseTypesLimit)
            {
                baseTypes.Add(context.WorkspaceResolver.CreateSymbolReference(current));
            }
        }

        BoundedCollection<TypeHierarchyNode>? derivedTypes = null;
        if (request.IncludeDerived)
        {
            var discoveredTypes = await FindDerivedTypeSymbolsAsync(namedType, context.CurrentSolution, context.CurrentSolution.Projects.ToImmutableHashSet(), cancellationToken);
            var uniqueTypes = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var typeReferences = new List<TypeHierarchyNode>();
            foreach (var discoveredType in discoveredTypes)
            {
                if (uniqueTypes.Add(discoveredType))
                {
                    var depth = GetTypeDepth(discoveredType, namedType);
                    if (depth > request.MaxDepth)
                    {
                        continue;
                    }

                    var reference = context.WorkspaceResolver.CreateSymbolReference(discoveredType);
                    typeReferences.Add(new TypeHierarchyNode
                    {
                        Type = reference,
                        Depth = depth,
                    });
                }
            }

            var orderedTypes = typeReferences.OrderBy(static item => item.Type?.DisplayName ?? string.Empty, StringComparer.Ordinal);

            var projectedTypes = new List<TypeHierarchyNode>();
            foreach (var typeNode in orderedTypes)
            {
                if (projectedTypes.Count == request.EffectiveDerivedTypesLimit)
                {
                    break;
                }

                projectedTypes.Add(typeNode);
            }

            derivedTypes = BoundedCollection.CreatePrebounded(projectedTypes, typeReferences.Count);
        }

        var orderedInterfaces = namedType.AllInterfaces
            .OrderBy(static item => item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal);

        var interfaces = new List<SymbolReference>();
        foreach (var interfaceSymbol in orderedInterfaces)
        {
            if (interfaces.Count == request.EffectiveInterfacesLimit)
            {
                break;
            }

            interfaces.Add(context.WorkspaceResolver.CreateSymbolReference(interfaceSymbol));
        }

        var type = context.WorkspaceResolver.CreateSymbolReference(namedType);
        var data = new TypeHierarchyData
        {
            Type = type,
            BaseTypes = BoundedCollection.CreatePrebounded(baseTypes, baseTypeCount),
            Interfaces = BoundedCollection.CreatePrebounded(interfaces, namedType.AllInterfaces.Length),
            DerivedTypes = derivedTypes,
        };

        return PluginExecutionResult.Success(data);
    }

    private static async ValueTask<IReadOnlyList<INamedTypeSymbol>> FindDerivedTypeSymbolsAsync(INamedTypeSymbol root, Solution solution, IImmutableSet<Project> projects, CancellationToken cancellationToken)
    {
        if (root.TypeKind == TypeKind.Interface)
        {
            return (await SymbolFinder.FindImplementationsAsync(root, solution, projects, cancellationToken))
                .OfType<INamedTypeSymbol>()
                .ToArray();
        }

        return (await SymbolFinder.FindDerivedClassesAsync(root, solution, projects, cancellationToken))
            .ToArray();
    }

    private static int GetTypeDepth(INamedTypeSymbol symbol, INamedTypeSymbol root)
    {
        var depth = 0;
        for (var current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            depth++;
            if (SymbolEqualityComparer.Default.Equals(current, root))
            {
                return depth;
            }
        }

        return depth;
    }
}
