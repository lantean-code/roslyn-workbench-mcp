namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-overloads", "Find Overloads", "Returns overload signatures for a resolved method or constructor.")]
internal sealed class FindOverloadsTool : QueryToolHandler<FindOverloadsRequest, OverloadSearchData>
{
    protected override async ValueTask<PluginExecutionResult<OverloadSearchData>> ExecuteCoreAsync(FindOverloadsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<OverloadSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not IMethodSymbol methodSymbol)
        {
            return ToolExecutionHelpers.Rejected<OverloadSearchData>("InvalidRequest", "Find overloads requires a method or constructor symbol.");
        }

        var overloads = methodSymbol.MethodKind == MethodKind.Constructor
            ? methodSymbol.ContainingType.InstanceConstructors.Where(static item => !item.IsImplicitlyDeclared)
            : methodSymbol.ContainingType.GetMembers(methodSymbol.Name).OfType<IMethodSymbol>().Where(item => item.MethodKind == methodSymbol.MethodKind);

        var orderedOverloads = overloads
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<IMethodSymbol>()
            .OrderBy(static item => item.Parameters.Length)
            .ThenBy(static item => item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal);

        var signatures = new List<CallableSignature>();
        var hasMore = false;
        foreach (var overload in orderedOverloads)
        {
            if (signatures.Count == request.EffectiveOverloadsLimit)
            {
                hasMore = true;
                break;
            }

            signatures.Add(CreateCallableSignature(overload));
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(methodSymbol);
        var data = new OverloadSearchData
        {
            Symbol = symbolReference,
            Overloads = ToolExecutionHelpers.CreatePreboundedCollection(signatures, hasMore),
        };

        return PluginExecutionResult<OverloadSearchData>.Success(data);
    }

    private static CallableSignature CreateCallableSignature(IMethodSymbol methodSymbol)
    {
        return new CallableSignature
        {
            DisplayName = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Kind = methodSymbol.MethodKind.ToString(),
            Parameters = methodSymbol.Parameters.Select(InspectionProjectionFactory.CreateParameterInfo).ToArray(),
            ReturnType = methodSymbol.MethodKind == MethodKind.Constructor ? null : InspectionProjectionFactory.CreateTypeInfo(methodSymbol.ReturnType),
        };
    }
}
