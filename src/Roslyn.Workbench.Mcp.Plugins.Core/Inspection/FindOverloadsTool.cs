using ContractTypeInfo = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.TypeInfo;

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
            return PluginExecutionResult.Rejected<OverloadSearchData>("InvalidRequest", "Find overloads requires a method or constructor symbol.");
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
