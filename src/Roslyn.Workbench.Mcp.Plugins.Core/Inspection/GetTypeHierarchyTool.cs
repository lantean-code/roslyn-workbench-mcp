using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-type-hierarchy", "Get Type Hierarchy", "Returns base, interface, and optional derived type relationships for a resolved type.")]
internal sealed class GetTypeHierarchyTool : QueryToolHandler<GetTypeHierarchyRequest, TypeHierarchyData>
{
    protected override async ValueTask<PluginExecutionResult<TypeHierarchyData>> ExecuteCoreAsync(GetTypeHierarchyRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        if (request.MaxDepth < 1)
        {
            return PluginExecutionResultFactory.Rejected<TypeHierarchyData>("InvalidRequest", "MaxDepth must be at least 1.");
        }

        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<TypeHierarchyData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return PluginExecutionResultFactory.Rejected<TypeHierarchyData>("InvalidRequest", "Get type hierarchy requires a named type symbol.");
        }

        var baseTypes = new List<SymbolReference>();
        var baseTypesHaveMore = false;
        var baseTypeDepth = 0;
        for (var current = namedType.BaseType; current is not null && baseTypeDepth < request.MaxDepth; current = current.BaseType)
        {
            baseTypeDepth++;
            if (baseTypes.Count == request.EffectiveBaseTypesLimit)
            {
                baseTypesHaveMore = true;
                break;
            }

            baseTypes.Add(context.WorkspaceResolver.CreateSymbolReference(current));
        }

        BoundedCollection<TypeHierarchyNode>? derivedTypes = null;
        if (request.IncludeDerived)
        {
            var discoveredTypes = await FindDerivedTypeSymbolsAsync(namedType, context.CurrentSolution, context.CurrentSolution.Projects.ToImmutableHashSet(), cancellationToken);
            var orderedTypes = discoveredTypes
                .Distinct(SymbolEqualityComparer.Default)
                .OfType<INamedTypeSymbol>()
                .Select(symbol => (Symbol: symbol, Reference: context.WorkspaceResolver.CreateSymbolReference(symbol)))
                .OrderBy(static item => item.Reference.DisplayName, StringComparer.Ordinal);

            var projectedTypes = new List<TypeHierarchyNode>();
            var derivedTypesHaveMore = false;
            foreach (var (symbol, reference) in orderedTypes)
            {
                var depth = GetTypeDepth(symbol, namedType);
                if (depth > request.MaxDepth)
                {
                    continue;
                }

                if (projectedTypes.Count == request.EffectiveDerivedTypesLimit)
                {
                    derivedTypesHaveMore = true;
                    break;
                }

                projectedTypes.Add(new TypeHierarchyNode
                {
                    Type = reference,
                    Depth = depth,
                });
            }

            derivedTypes = BoundedCollection<TypeHierarchyNode>.CreatePrebounded(projectedTypes, derivedTypesHaveMore);
        }

        var orderedInterfaces = namedType.AllInterfaces
            .OrderBy(static item => item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal);

        var interfaces = new List<SymbolReference>();
        var interfacesHaveMore = false;
        foreach (var interfaceSymbol in orderedInterfaces)
        {
            if (interfaces.Count == request.EffectiveInterfacesLimit)
            {
                interfacesHaveMore = true;
                break;
            }

            interfaces.Add(context.WorkspaceResolver.CreateSymbolReference(interfaceSymbol));
        }

        var type = context.WorkspaceResolver.CreateSymbolReference(namedType);
        var data = new TypeHierarchyData
        {
            Type = type,
            BaseTypes = BoundedCollection<SymbolReference>.CreatePrebounded(baseTypes, baseTypesHaveMore),
            Interfaces = BoundedCollection<SymbolReference>.CreatePrebounded(interfaces, interfacesHaveMore),
            DerivedTypes = derivedTypes,
        };

        return PluginExecutionResult<TypeHierarchyData>.Success(data);
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
