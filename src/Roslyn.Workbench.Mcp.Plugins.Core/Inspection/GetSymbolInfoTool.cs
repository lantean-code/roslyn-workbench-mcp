using ContractTypeInfo = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.TypeInfo;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-symbol-info", "Get Symbol Info", "Returns detailed metadata for a resolved symbol.")]
internal sealed class GetSymbolInfoTool : QueryToolHandler<GetSymbolInfoRequest, SymbolInfoData>
{
    protected override async ValueTask<PluginExecutionResult<SymbolInfoData>> ExecuteCoreAsync(GetSymbolInfoRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<SymbolInfoData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        IReadOnlyList<ParameterInfo>? parameters = null;
        ContractTypeInfo? returnType = null;
        if (symbol is IMethodSymbol methodSymbol)
        {
            var projectedParameters = new List<ParameterInfo>();
            foreach (var parameter in methodSymbol.Parameters)
            {
                projectedParameters.Add(InspectionProjectionFactory.CreateParameterInfo(parameter));
            }

            parameters = projectedParameters;
            returnType = InspectionProjectionFactory.CreateTypeInfo(methodSymbol.ReturnType);
        }

        var declarations = new List<ResolvedLocation>();
        foreach (var location in symbol.Locations)
        {
            if (!location.IsInSource)
            {
                continue;
            }

            var declaration = context.WorkspaceResolver.CreateResolvedLocation(location);
            if (declaration is not null)
            {
                declarations.Add(declaration);
            }
        }

        var orderedDeclarations = declarations
            .OrderBy(static location => location.Document?.Path, StringComparer.Ordinal)
            .ThenBy(static location => location.Span?.Start)
            .ToArray();

        var data = new SymbolInfoData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            Modifiers = InspectionProjectionFactory.GetModifiers(symbol),
            Type = InspectionProjectionFactory.CreateAssociatedTypeInfo(symbol),
            Parameters = parameters,
            ReturnType = returnType,
            Documentation = request.IncludeDocumentation ? symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken) : null,
            Declarations = orderedDeclarations,
        };

        return PluginExecutionResult.Success(data);
    }
}
