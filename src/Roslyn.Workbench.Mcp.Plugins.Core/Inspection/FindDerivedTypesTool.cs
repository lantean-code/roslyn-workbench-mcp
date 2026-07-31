namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-derived-types", "Find Derived Types", "Finds derived types for a resolved base type.")]
internal sealed class FindDerivedTypesTool : QueryToolHandler<FindDerivedTypesRequest, DerivedTypesData>
{
    protected override async ValueTask<PluginExecutionResult<DerivedTypesData>> ExecuteCoreAsync(FindDerivedTypesRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<DerivedTypesData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return PluginExecutionResult.Rejected<DerivedTypesData>("InvalidRequest", "Find derived types requires a named type symbol.");
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
