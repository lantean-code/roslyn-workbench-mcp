namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns the declarations for a partial type or method.
/// </summary>
[RoslynTool("get-partial-declarations", "Get Partial Declarations", "Returns source declaration locations for a resolved symbol, ordered by document path and source position. Use this to navigate split partial type declarations; use get-symbol-info for symbol metadata or find-references for usages. Only declarations belonging to the resolved symbol are returned: partial method definition and implementation parts are not combined automatically. Metadata-only or implicitly declared symbols may return no declarations. Defaults to 32 locations; check declarations.hasMore before treating the results as complete.")]
internal sealed class GetPartialDeclarationsTool : QueryToolHandler<GetPartialDeclarationsRequest, PartialDeclarationsData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<PartialDeclarationsData>> ExecuteCoreAsync(GetPartialDeclarationsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<PartialDeclarationsData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var resolvedLocations = new List<ResolvedLocation>();
        foreach (var declaration in symbol.DeclaringSyntaxReferences)
        {
            var location = declaration.SyntaxTree.GetLocation(declaration.Span);
            var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
            if (resolvedLocation is not null)
            {
                resolvedLocations.Add(resolvedLocation);
            }
        }

        var orderedLocations = resolvedLocations
            .OrderBy(static location => location.Document?.Path, StringComparer.Ordinal)
            .ThenBy(static location => location.Span?.Start);

        var declarations = new List<ResolvedLocation>();
        foreach (var resolvedLocation in orderedLocations)
        {
            if (declarations.Count == request.EffectiveDeclarationsLimit)
            {
                break;
            }

            declarations.Add(resolvedLocation);
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new PartialDeclarationsData
        {
            Symbol = symbolReference,
            Declarations = BoundedCollection.CreatePrebounded(declarations, resolvedLocations.Count),
        };

        return PluginExecutionResult.Success(data);
    }
}
