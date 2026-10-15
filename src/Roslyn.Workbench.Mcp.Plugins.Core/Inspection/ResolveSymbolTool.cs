namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Resolves the symbol at a location or selection and returns its canonical selector.
/// </summary>
[RoslynTool("resolve-symbol", "Resolve Symbol", "Resolves the symbol at a source location or selection, returning a symbol reference, a reusable selector when available and bounded source declarations. Use this to turn a known location into a target for symbol inspection; use search-symbols when only a name is known, or get-symbol-info for detailed metadata. Echo the location snapshot in expectedSnapshot to reject stale locations. Returns up to 32 source declarations by default; check declarations.hasMore. Metadata-only symbols may have no source declarations.")]
internal sealed class ResolveSymbolTool : QueryToolHandler<ResolveSymbolRequest, ResolveSymbolData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<ResolveSymbolData>> ExecuteCoreAsync(ResolveSymbolRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<ResolveSymbolData>(context, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken);
        if (!locationResolution.IsResolved)
        {
            return SelectorRejectionFactory.Create<ResolveSymbolData>(locationResolution.Status, "Location", "location");
        }

        var symbolResolution = await context.WorkspaceResolver.ResolveSymbolAsync(new SymbolSelector
        {
            Location = request.Location,
        }, cancellationToken);

        if (!symbolResolution.IsResolved)
        {
            return SelectorRejectionFactory.Create<ResolveSymbolData>(symbolResolution.Status, "Symbol", "symbol");
        }

        var symbol = symbolResolution.Value;
        SymbolSelector? selector = null;
        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is not null)
        {
            var resolvedSourceLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
            selector = context.ToolExecutionServices.WorkspaceSelectorFactory.CreateSymbolSelector(resolvedSourceLocation);
        }

        if (selector is null)
        {
            var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(locationResolution.Value);
            selector = context.ToolExecutionServices.WorkspaceSelectorFactory.CreateSymbolSelector(resolvedLocation);
        }

        var declarations = CreateDeclarations(
            symbol,
            request.EffectiveDeclarationsLimit,
            context.WorkspaceResolver);

        var data = new ResolveSymbolData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Selector = selector,
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
