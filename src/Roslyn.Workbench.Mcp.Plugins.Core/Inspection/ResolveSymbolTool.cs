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
        var snapshotRejection = context.ToolExecutionServices.RequestResolver.ValidateSnapshot<ResolveSymbolData>(context, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Location is null)
        {
            return ToolExecutionHelpers.Rejected<ResolveSymbolData>("InvalidRequest", "Resolve symbol requires location.");
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken).ConfigureAwait(false);
        if (locationResolution.Status != SelectorResolveStatus.Resolved)
        {
            return ToolExecutionHelpers.RejectFromStatus<ResolveSymbolData>(locationResolution.Status, "Location");
        }

        var symbolResolution = await context.WorkspaceResolver.ResolveSymbolAsync(new SymbolSelector
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
            Symbol = context.WorkspaceResolver.CreateSymbolReference(symbol),
            Selector = ToolExecutionHelpers.CreateSourceSymbolSelector(symbol, context.WorkspaceResolver)
                ?? ToolExecutionHelpers.CreateLocationSymbolSelector(context.WorkspaceResolver.CreateResolvedLocation(locationResolution.Value!)),
            Declarations = symbol.Locations
                .Where(static location => location.IsInSource)
                .Select(location => context.WorkspaceResolver.CreateResolvedLocation(location))
                .Where(static location => location is not null)
                .Select(static location => location!)
                .OrderBy(static location => location.Document!.Path, StringComparer.Ordinal)
                .ThenBy(static location => location.Span!.Start)
                .ToArray(),
        };

        return PluginExecutionResult<ResolveSymbolData>.Success(data);
    }
}
