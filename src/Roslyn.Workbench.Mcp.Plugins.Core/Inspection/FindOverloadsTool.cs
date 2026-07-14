using Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-overloads", "Find Overloads", "Returns overload signatures for a resolved method or constructor.")]
internal sealed class FindOverloadsTool : QueryToolHandler<FindOverloadsRequest, OverloadSearchData>
{
    protected override async ValueTask<PluginExecutionResult<OverloadSearchData>> ExecuteCoreAsync(FindOverloadsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<OverloadSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not IMethodSymbol methodSymbol)
        {
            return ToolExecutionHelpers.Rejected<OverloadSearchData>("InvalidRequest", "Find overloads requires a method or constructor symbol.");
        }

        IEnumerable<IMethodSymbol> overloads = methodSymbol.MethodKind == MethodKind.Constructor
            ? methodSymbol.ContainingType.InstanceConstructors.Where(static item => !item.IsImplicitlyDeclared)
            : methodSymbol.ContainingType.GetMembers(methodSymbol.Name).OfType<IMethodSymbol>().Where(item => item.MethodKind == methodSymbol.MethodKind);
        var signatures = overloads
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<IMethodSymbol>()
            .OrderBy(static item => item.Parameters.Length)
            .ThenBy(static item => item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal)
            .Select(CreateCallableSignature)
            .ToArray();

        return PluginExecutionResult<OverloadSearchData>.Success(new OverloadSearchData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(methodSymbol),
            Overloads = ToolExecutionHelpers.CreateBoundedCollection(
                signatures,
                ToolExecutionHelpers.GetMaxResults(context, request.OverloadsLimit)),
        });
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
