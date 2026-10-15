using ContractTypeInfo = Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection.TypeInfo;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns detailed metadata for a resolved symbol.
/// </summary>
[RoslynTool("get-symbol-info", "Get Symbol Info", "Inspects one resolved symbol, returning accessibility, modifiers, associated type and source declaration locations, plus method parameters and return type when applicable. Use this after search-symbols or resolve-symbol to understand a chosen declaration; use find-references for its usages or get-symbol-members to enumerate type members. XML documentation is opt-in through includeDocumentation. Defaults to 32 declarations and 64 method parameters, bounded independently; check declarations.hasMore and parameters.hasMore when present. Metadata-only symbols may have no source declarations.")]
internal sealed class GetSymbolInfoTool : QueryToolHandler<GetSymbolInfoRequest, SymbolInfoData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<SymbolInfoData>> ExecuteCoreAsync(GetSymbolInfoRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<SymbolInfoData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        BoundedCollection<ParameterInfo>? parameters = null;
        ContractTypeInfo? returnType = null;
        if (symbol is IMethodSymbol methodSymbol)
        {
            var projectedParameters = new List<ParameterInfo>();
            foreach (var parameter in methodSymbol.Parameters)
            {
                if (projectedParameters.Count == request.EffectiveParametersLimit)
                {
                    break;
                }

                projectedParameters.Add(InspectionProjectionFactory.CreateParameterInfo(parameter));
            }

            parameters = BoundedCollection.CreatePrebounded(projectedParameters, methodSymbol.Parameters.Length);
            returnType = InspectionProjectionFactory.CreateTypeInfo(methodSymbol.ReturnType);
        }

        var modifiers = InspectionProjectionFactory.GetModifiers(symbol);
        var declarations = CreateDeclarations(
            symbol,
            request.EffectiveDeclarationsLimit,
            context.WorkspaceResolver);

        var data = new SymbolInfoData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            Modifiers = modifiers,
            Type = InspectionProjectionFactory.CreateAssociatedTypeInfo(symbol),
            Parameters = parameters,
            ReturnType = returnType,
            Documentation = request.IncludeDocumentation ? symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken) : null,
            Declarations = declarations,
        };

        return PluginExecutionResult.Success(data);
    }

    private static BoundedCollection<ResolvedLocation> CreateDeclarations(
        ISymbol symbol,
        int maxResults,
        IWorkspaceResolver workspaceResolver)
    {
        var orderedLocations = symbol.Locations
            .Where(static location => location.IsInSource)
            .OrderBy(static location => location.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(static location => location.SourceSpan.Start);

        var declarations = new List<ResolvedLocation>();
        foreach (var location in orderedLocations)
        {
            var declaration = workspaceResolver.CreateResolvedLocation(location);
            if (declaration is null)
            {
                continue;
            }

            if (declarations.Count == maxResults)
            {
                return BoundedCollection.CreatePrebounded(declarations, hasMore: true);
            }

            declarations.Add(declaration);
        }

        return BoundedCollection.CreatePrebounded(declarations, hasMore: false);
    }
}
