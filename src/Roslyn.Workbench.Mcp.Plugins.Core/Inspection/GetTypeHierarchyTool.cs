namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns base, interface, and optional derived type relationships for a resolved type.
/// </summary>
[RoslynTool("get-type-hierarchy", "Get Type Hierarchy", "Inspects the inheritance neighbourhood of a resolved named type, returning type, baseTypes, interfaces and derivedTypes. Use this for a combined hierarchy view; use find-derived-types when you need a scoped descendant search. Derived types are included by default and searched across the loaded solution; includeDerived=false omits that collection. maxDepth defaults to 3 for base and derived types, with direct relationships at depth 1; it does not limit the transitive interface list. Independent result limits default to 16 base types, 64 interfaces and 100 derived types. Check each collection.hasMore for result truncation; base and derived counts describe only the requested depth. Read-only; no code is executed.")]
internal sealed class GetTypeHierarchyTool : QueryToolHandler<GetTypeHierarchyRequest, TypeHierarchyData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<TypeHierarchyData>> ExecuteCoreAsync(GetTypeHierarchyRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<TypeHierarchyData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return PluginExecutionResult.Rejected<TypeHierarchyData>(PluginErrorCodes.InvalidRequest, "Get type hierarchy requires a named type symbol.");
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
            var discoveredTypes = await context.ToolExecutionServices.TypeHierarchyService.FindDerivedTypesAsync(
                namedType,
                context.CurrentSolution,
                context.CurrentSolution.Projects.ToArray(),
                cancellationToken);
            var typeReferences = new List<TypeHierarchyNode>();
            foreach (var discoveredType in discoveredTypes)
            {
                if (discoveredType.Depth > request.MaxDepth)
                {
                    continue;
                }

                var reference = context.WorkspaceResolver.CreateSymbolReference(discoveredType.Type);
                typeReferences.Add(new TypeHierarchyNode
                {
                    Type = reference,
                    Depth = discoveredType.Depth,
                });
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
}
