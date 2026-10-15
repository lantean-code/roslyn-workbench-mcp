using ContractTypeInfo = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.TypeInfo;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns overload signatures for a resolved method or constructor.
/// </summary>
[RoslynTool("find-overloads", "Find Overloads", "Lists callable signatures for a resolved method or constructor so you can compare parameter and return types. Requires a callable symbol; use search-symbols to discover one first. Methods are matched by name and method kind within the same containing type; instance constructors include only explicitly declared constructors. The selected callable can appear in the results. This does not resolve a call against arguments or collect inherited overloads. Returns symbol and a bounded overloads collection ordered by parameter count then signature. overloadsLimit defaults to 50; check overloads.hasMore for truncation. Reads symbols without invoking code.")]
internal sealed class FindOverloadsTool : QueryToolHandler<FindOverloadsRequest, OverloadSearchData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<OverloadSearchData>> ExecuteCoreAsync(FindOverloadsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<OverloadSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not IMethodSymbol methodSymbol)
        {
            return PluginExecutionResult.Rejected<OverloadSearchData>(PluginErrorCodes.InvalidRequest, "Find overloads requires a method or constructor symbol.");
        }

        var overloads = new List<IMethodSymbol>();
        if (methodSymbol.MethodKind == MethodKind.Constructor)
        {
            foreach (var constructor in methodSymbol.ContainingType.InstanceConstructors)
            {
                if (!constructor.IsImplicitlyDeclared)
                {
                    overloads.Add(constructor);
                }
            }
        }
        else
        {
            foreach (var member in methodSymbol.ContainingType.GetMembers(methodSymbol.Name))
            {
                if (member is IMethodSymbol overload
                    && overload.MethodKind == methodSymbol.MethodKind)
                {
                    overloads.Add(overload);
                }
            }
        }

        var uniqueOverloads = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var distinctOverloads = new List<IMethodSymbol>();
        foreach (var overload in overloads)
        {
            if (uniqueOverloads.Add(overload))
            {
                distinctOverloads.Add(overload);
            }
        }

        var orderedOverloads = distinctOverloads
            .OrderBy(static item => item.Parameters.Length)
            .ThenBy(static item => item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal);

        var signatures = new List<CallableSignature>();
        foreach (var overload in orderedOverloads)
        {
            if (signatures.Count == request.EffectiveOverloadsLimit)
            {
                break;
            }

            signatures.Add(CreateCallableSignature(overload));
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(methodSymbol);
        var data = new OverloadSearchData
        {
            Symbol = symbolReference,
            Overloads = BoundedCollection.CreatePrebounded(signatures, distinctOverloads.Count),
        };

        return PluginExecutionResult.Success(data);
    }

    private static CallableSignature CreateCallableSignature(IMethodSymbol methodSymbol)
    {
        var parameters = new List<ParameterInfo>();
        foreach (var parameter in methodSymbol.Parameters)
        {
            parameters.Add(InspectionProjectionFactory.CreateParameterInfo(parameter));
        }

        ContractTypeInfo? returnType = null;
        if (methodSymbol.MethodKind != MethodKind.Constructor)
        {
            returnType = InspectionProjectionFactory.CreateTypeInfo(methodSymbol.ReturnType);
        }

        return new CallableSignature
        {
            DisplayName = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Kind = methodSymbol.MethodKind.ToString(),
            Parameters = parameters,
            ReturnType = returnType,
        };
    }
}
