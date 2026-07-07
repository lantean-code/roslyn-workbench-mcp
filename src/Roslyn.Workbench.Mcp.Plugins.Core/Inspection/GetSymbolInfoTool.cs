using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class GetSymbolInfoTool : QueryToolHandler<GetSymbolInfoRequest, SymbolInfoData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-symbol-info",
        Title = "Get Symbol Info",
        Description = "Returns detailed metadata for a resolved symbol.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetSymbolInfoTool());
    }

    protected override async ValueTask<PluginExecutionResult<SymbolInfoData>> ExecuteCoreAsync(GetSymbolInfoRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
                .Where(static location => location is not null)
                .Select(static location => location!)
                .OrderBy(static location => location.Document!.Path, StringComparer.Ordinal)
                .ThenBy(static location => location.Span!.Start)
                .ToArray(),
        };

        return PluginExecutionResult<SymbolInfoData>.Success(data);
    }
}
