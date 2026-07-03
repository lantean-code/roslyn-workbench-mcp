using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class ResolveSymbolTool : QueryToolHandler<ResolveSymbolRequest, ResolveSymbolData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "resolve-symbol",
        Title = "Resolve Symbol",
        Description = "Resolves the symbol at a location or selection and returns its canonical selector.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new ResolveSymbolTool());
    }

    protected override async ValueTask<PluginExecutionResult<ResolveSymbolData>> ExecuteCoreAsync(ResolveSymbolRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshotRejection = ToolExecutionHelpers.ValidateSnapshot<ResolveSymbolData>(context, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Location is null)
        {
            return ToolExecutionHelpers.Rejected<ResolveSymbolData>("InvalidRequest", "Resolve symbol requires location.");
        }

        var locationResolution = await context.Resolver.ResolveLocationAsync(request.Location, cancellationToken).ConfigureAwait(false);
        if (locationResolution.Status != SelectorResolveStatus.Resolved)
        {
            return ToolExecutionHelpers.RejectFromStatus<ResolveSymbolData>(locationResolution.Status, "Location");
        }

        var symbolResolution = await context.Resolver.ResolveSymbolAsync(new SymbolSelector
        {
            Location = request.Location,
        }, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.Status != SelectorResolveStatus.Resolved)
        {
            return ToolExecutionHelpers.RejectFromStatus<ResolveSymbolData>(symbolResolution.Status, "Symbol");
        }

        var symbol = symbolResolution.Value!;
        var data = new ResolveSymbolData
        {
            Symbol = context.Resolver.CreateSymbolReference(symbol),
            Selector = ToolExecutionHelpers.CreateSourceSymbolSelector(symbol, context.Resolver)
                ?? ToolExecutionHelpers.CreateLocationSymbolSelector(context.Resolver.CreateResolvedLocation(locationResolution.Value!)),
            Declarations = symbol.Locations
                .Where(static location => location.IsInSource)
                .Select(location => context.Resolver.CreateResolvedLocation(location))
                .Where(static location => location is not null)
                .Select(static location => location!)
                .OrderBy(static location => location.Document!.Path, StringComparer.Ordinal)
                .ThenBy(static location => location.Span!.Start)
                .ToArray(),
        };

        return ToolExecutionHelpers.EnsureWithinSize(context, data);
    }
}
