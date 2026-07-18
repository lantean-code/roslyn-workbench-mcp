namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-symbol-info", "Get Symbol Info", "Returns detailed metadata for a resolved symbol.")]
internal sealed class GetSymbolInfoTool : QueryToolHandler<GetSymbolInfoRequest, SymbolInfoData>
{
    protected override async ValueTask<PluginExecutionResult<SymbolInfoData>> ExecuteCoreAsync(GetSymbolInfoRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<SymbolInfoData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var data = new SymbolInfoData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            Modifiers = InspectionProjectionFactory.GetModifiers(symbol),
            Type = InspectionProjectionFactory.CreateAssociatedTypeInfo(symbol),
            Parameters = symbol is IMethodSymbol methodSymbol ? methodSymbol.Parameters.Select(InspectionProjectionFactory.CreateParameterInfo).ToArray() : null,
            ReturnType = symbol is IMethodSymbol method ? InspectionProjectionFactory.CreateTypeInfo(method.ReturnType) : null,
            Documentation = request.IncludeDocumentation ? symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken) : null,
            Declarations = symbol.Locations
                .Where(static location => location.IsInSource)
                .Select(location => context.WorkspaceResolver.CreateResolvedLocation(location))
                .OfType<ResolvedLocation>()
                .OrderBy(static location => location.Document?.Path, StringComparer.Ordinal)
                .ThenBy(static location => location.Span?.Start)
                .ToArray(),
        };

        return PluginExecutionResult<SymbolInfoData>.Success(data);
    }
}
