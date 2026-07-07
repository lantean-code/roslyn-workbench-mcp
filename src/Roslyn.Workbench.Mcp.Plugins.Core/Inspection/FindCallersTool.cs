using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

internal sealed class FindCallersTool : QueryToolHandler<FindCallersRequest, CallerSearchData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "find-callers",
        Title = "Find Callers",
        Description = "Returns direct source call sites and containing symbols.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new FindCallersTool());
    }

    protected override async ValueTask<PluginExecutionResult<CallerSearchData>> ExecuteCoreAsync(FindCallersRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<CallerSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        var symbol = symbolResolution.Value;
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<CallerSearchData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var discoveredCallers = await SymbolFinder.FindCallersAsync(symbol, context.CurrentSolution, documents.Value.ToImmutableHashSet(), cancellationToken).ConfigureAwait(false);
        var callers = new List<CallerInfo>();
        foreach (var caller in discoveredCallers)
        {
            var contexts = new List<string>();
            if (request.IncludeContext)
            {
                foreach (var location in caller.Locations.Where(static location => location.IsInSource))
                {
                    var contextLine = await context.ToolExecutionServices.InspectionContextService.ReadContextAsync(context.CurrentSolution.GetDocument(location.SourceTree!), location.SourceSpan, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(contextLine))
                    {
                        contexts.Add(contextLine);
                    }
                }
            }

            callers.Add(new CallerInfo
            {
                Caller = context.WorkspaceResolver.CreateSymbolReference(caller.CallingSymbol),
                Locations = caller.Locations
                    .Where(static location => location.IsInSource)
                    .Select(location => context.WorkspaceResolver.CreateResolvedLocation(location))
                    .Where(static location => location is not null)
                    .Select(static location => location!)
                    .OrderBy(static location => location.Document!.Path, StringComparer.Ordinal)
                    .ThenBy(static location => location.Span!.Start)
                    .ToArray(),
                Contexts = request.IncludeContext ? contexts.ToArray() : [],
            });
        }

        var orderedCallers = callers
            .OrderBy(static caller => caller.Caller!.DisplayName, StringComparer.Ordinal)
            .ToArray();
        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);

        return PluginExecutionResult<CallerSearchData>.Success(new CallerSearchData
        {
            Symbol = symbolReference,
            Callers = ToolExecutionHelpers.CreateBoundedCollection(
                orderedCallers,
                ToolExecutionHelpers.GetMaxResults(context, request.CallersLimit)),
        });
    }
}
