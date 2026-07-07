using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class FindOverloadsTool : QueryToolHandler<FindOverloadsRequest, OverloadSearchData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-overloads",
        Title = "Find Overloads",
        Description = "Returns overload signatures for a resolved method or constructor.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindOverloadsTool());
    }

    protected override async ValueTask<PluginExecutionResult<OverloadSearchData>> ExecuteCoreAsync(FindOverloadsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
