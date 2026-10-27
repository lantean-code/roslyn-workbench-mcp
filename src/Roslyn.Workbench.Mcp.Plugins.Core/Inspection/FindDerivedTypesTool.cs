namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Finds derived types for a resolved base type.
/// </summary>
[RoslynTool("find-derived-types", "Find Derived Types", "Finds source types derived from a resolved named type, with their inheritance distance. Use this to explore descendants; use get-type-hierarchy for base types as well, or find-implementations for interface member implementations. For an interface, includes derived interfaces and implementing types. Searches projects selected by scope, or the loaded solution when scope is omitted. Defaults to three inheritance levels and 100 returned types. Check derivedTypes.hasMore for truncation within maxDepth; deeper types are excluded.")]
internal sealed class FindDerivedTypesTool : QueryToolHandler<FindDerivedTypesRequest, DerivedTypesData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<DerivedTypesData>> ExecuteCoreAsync(FindDerivedTypesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<DerivedTypesData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return PluginExecutionResult.Rejected<DerivedTypesData>(PluginErrorCodes.InvalidRequest, "Find derived types requires a named type symbol.");
        }

        var scopeResolution = context.ToolExecutionServices.RequestResolver.ResolveProjects<DerivedTypesData>(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var discoveredTypes = await context.ToolExecutionServices.TypeHierarchyService.FindDerivedTypesAsync(
            namedType,
            context.CurrentSolution,
            scopeResolution.Value,
            cancellationToken);
        var projectedTypes = new List<TypeHierarchyNode>();
        foreach (var discoveredType in discoveredTypes)
        {
            if (discoveredType.Depth > request.MaxDepth)
            {
                continue;
            }

            var reference = context.WorkspaceResolver.CreateSymbolReference(discoveredType.Type);
            projectedTypes.Add(new TypeHierarchyNode
            {
                Type = reference,
                Depth = discoveredType.Depth,
            });
        }

        var orderedTypes = projectedTypes.OrderBy(static item => item.Type?.DisplayName ?? string.Empty, StringComparer.Ordinal);

        var derivedTypes = new List<TypeHierarchyNode>();
        foreach (var type in orderedTypes)
        {
            if (derivedTypes.Count == request.EffectiveDerivedTypesLimit)
            {
                break;
            }

            derivedTypes.Add(type);
        }

        var baseType = context.WorkspaceResolver.CreateSymbolReference(namedType);
        var data = new DerivedTypesData
        {
            BaseType = baseType,
            DerivedTypes = BoundedCollection.CreatePrebounded(derivedTypes, projectedTypes.Count),
        };

        return PluginExecutionResult.Success(data);
    }
}
