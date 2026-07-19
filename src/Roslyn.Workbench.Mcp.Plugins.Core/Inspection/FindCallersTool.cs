using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("find-callers", "Find Callers", "Returns direct source call sites and containing symbols.")]
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
            .OrderBy(static item => item.Reference.DisplayName, StringComparer.Ordinal);

        var callers = new List<CallerInfo>();
        var hasMore = false;
        foreach (var (caller, reference) in orderedCallers)
        {
            if (callers.Count == request.EffectiveCallersLimit)
            {
                hasMore = true;
                break;
            }

            var contexts = new List<string>();
            if (request.IncludeContext)
            {
                foreach (var location in caller.Locations.Where(static location => location.IsInSource))
                {
                    var document = location.SourceTree is null
                        ? null
                        : context.CurrentSolution.GetDocument(location.SourceTree);

                    var contextLine = await context.ToolExecutionServices.InspectionContextService.ReadContextAsync(document, location.SourceSpan, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(contextLine))
                    {
                        contexts.Add(contextLine);
                    }
                }
            }

            callers.Add(new CallerInfo
            {
                Caller = reference,
                Locations = caller.Locations
                    .Where(static location => location.IsInSource)
                    .Select(location => context.WorkspaceResolver.CreateResolvedLocation(location))
                    .OfType<ResolvedLocation>()
                    .OrderBy(static location => location.Document?.Path, StringComparer.Ordinal)
                    .ThenBy(static location => location.Span?.Start)
                    .ToArray(),
                Contexts = request.IncludeContext ? contexts.ToArray() : [],
            });
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(symbol);
        var data = new CallerSearchData
        {
            Symbol = symbolReference,
            Callers = ToolExecutionHelpers.CreatePreboundedCollection(callers, hasMore),
        };

        return PluginExecutionResult<CallerSearchData>.Success(data);
    }
}
