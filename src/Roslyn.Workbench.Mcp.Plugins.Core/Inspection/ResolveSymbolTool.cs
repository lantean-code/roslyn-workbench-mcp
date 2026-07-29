namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("resolve-symbol", "Resolve Symbol", "Resolves the symbol at a location or selection and returns its canonical selector.")]
internal sealed class ResolveSymbolTool : QueryToolHandler<ResolveSymbolRequest, ResolveSymbolData>
{
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

        var data = new ResolveSymbolData
        {
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Selector = selector,
            Declarations = orderedDeclarations,
        };

        return PluginExecutionResult.Success(data);
    }
}
