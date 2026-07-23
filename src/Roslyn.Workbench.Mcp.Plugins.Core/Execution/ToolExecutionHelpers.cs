namespace Roslyn.Workbench.Mcp.Plugins.Core.Execution;

internal static class ToolExecutionHelpers
{
    public static int GetMaxResults(int? requestLimit, int defaultMaxResults)
    {
        return Math.Max(0, requestLimit ?? defaultMaxResults);
    }

    public static IReadOnlyList<T> ApplyLimit<T>(IReadOnlyList<T> items, int maxResults, out bool hasMore)
    {
        hasMore = items.Count > maxResults;
        return hasMore ? items.Take(maxResults).ToArray() : items;
    }

    public static SymbolSelector? CreateSourceSymbolSelector(ISymbol symbol, IWorkspaceResolver resolver)
    {
        var sourceLocation = symbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return null;
        }

        var resolvedLocation = resolver.CreateResolvedLocation(sourceLocation);
        return CreateLocationSymbolSelector(resolvedLocation);
    }

    public static SymbolSelector? CreateLocationSymbolSelector(ResolvedLocation? resolvedLocation)
    {
        if (resolvedLocation?.Document is null || resolvedLocation.Span is null)
        {
            return null;
        }

        var span = new TextSpanSelector
        {
            Document = CreateDocumentSelector(resolvedLocation.Document),
            Start = resolvedLocation.Span.Start,
            Length = resolvedLocation.Span.Length,
        };

        var location = new LocationSelector
        {
            Span = span,
        };

        return new SymbolSelector
        {
            Location = location,
        };
    }

    public static LocationSelector? CreateLocationSelector(ResolvedLocation? resolvedLocation)
    {
        if (resolvedLocation?.Document is null || resolvedLocation.Span is null)
        {
            return null;
        }

        var span = new TextSpanSelector
        {
            Document = CreateDocumentSelector(resolvedLocation.Document),
            Start = resolvedLocation.Span.Start,
            Length = resolvedLocation.Span.Length,
        };

        return new LocationSelector
        {
            Span = span,
        };
    }

    private static DocumentSelector CreateDocumentSelector(DocumentReference document)
    {
        if (!string.IsNullOrWhiteSpace(document.DocumentId))
        {
            return new DocumentSelector
            {
                DocumentId = document.DocumentId,
            };
        }

        return new DocumentSelector
        {
            Path = document.Path,
        };
    }
}
