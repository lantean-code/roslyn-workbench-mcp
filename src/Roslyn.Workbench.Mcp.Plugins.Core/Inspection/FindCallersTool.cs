using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-callers", "Find Callers", "Returns bounded direct source call sites and containing symbols.")]
internal sealed class FindCallersTool : QueryToolHandler<FindCallersRequest, CallerSearchData>
{
    protected override async ValueTask<PluginExecutionResult<CallerSearchData>> ExecuteCoreAsync(FindCallersRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<CallerSearchData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
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

        var discoveredCallers = await SymbolFinder.FindCallersAsync(symbol, context.CurrentSolution, documents.Value.ToImmutableHashSet(), cancellationToken);
        var orderedCallers = discoveredCallers
            .Select(caller => (Caller: caller, Reference: context.WorkspaceResolver.CreateSymbolReference(caller.CallingSymbol)))
            .OrderBy(static item => item.Reference.DisplayName, StringComparer.Ordinal)
            .ToArray();

        var callers = new List<CallerInfo>();
        foreach (var (caller, reference) in orderedCallers)
        {
            if (callers.Count == request.EffectiveCallersLimit)
            {
                break;
            }

            var callSiteCandidates = new List<(Location Source, ResolvedLocation Resolved)>();
            foreach (var location in caller.Locations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!location.IsInSource)
                {
                    continue;
                }

                var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(location);
                if (resolvedLocation is not null)
                {
                    callSiteCandidates.Add((location, resolvedLocation));
                }
            }

            var orderedCallSiteCandidates = callSiteCandidates
                .OrderBy(static item => item.Resolved.Document?.Path, StringComparer.Ordinal)
                .ThenBy(static item => item.Resolved.Span?.Start)
                .ToArray();

            var callSites = new List<CallerSiteInfo>();
            foreach (var (sourceLocation, resolvedLocation) in orderedCallSiteCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (callSites.Count == request.EffectiveCallSitesPerCallerLimit)
                {
                    break;
                }

                string? contextLine = null;
                if (request.IncludeContext)
                {
                    var document = sourceLocation.SourceTree is null
                        ? null
                        : context.CurrentSolution.GetDocument(sourceLocation.SourceTree);

                    contextLine = await context.ToolExecutionServices.InspectionContextService.ReadContextAsync(document, sourceLocation.SourceSpan, cancellationToken);
                    if (string.IsNullOrWhiteSpace(contextLine))
                    {
                        contextLine = null;
                    }
                }

                callSites.Add(new CallerSiteInfo
                {
                    Location = resolvedLocation,
                    Context = contextLine,
                });
            }

            callers.Add(new CallerInfo
            {
                Caller = reference,
                CallSites = BoundedCollection.CreatePrebounded(callSites, orderedCallSiteCandidates.Length),
            });
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new CallerSearchData
        {
            Symbol = symbolReference,
            Callers = BoundedCollection.CreatePrebounded(callers, orderedCallers.Length),
        };

        return PluginExecutionResult.Success(data);
    }
}
