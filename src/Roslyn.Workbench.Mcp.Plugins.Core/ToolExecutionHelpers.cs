namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal static class ToolExecutionHelpers
{
    public static int GetMaxResults(int? requestLimit, int defaultMaxResults)
    {
        return requestLimit ?? defaultMaxResults;
    }

    public static BoundedCollection<T> CreateBoundedCollection<T>(
        IReadOnlyList<T> items,
        int maxResults)
    {

        return BoundedCollection<T>.Create(items, maxResults);
    }

    public static BoundedCollection<T> CreatePreboundedCollection<T>(
        IReadOnlyList<T> items,
        bool hasMore)
    {
        if (items.Count == 0 && !hasMore)
        {
            return BoundedCollection<T>.Empty();
        }

        return new BoundedCollection<T>
        {
            Items = items,
            HasMore = hasMore,
        };
    }

    public static IReadOnlyList<T> ApplyLimit<T>(IReadOnlyList<T> items, int maxResults, out bool hasMore)
    {

        hasMore = items.Count > maxResults;
        return hasMore ? items.Take(maxResults).ToArray() : items;
    }

    public static PluginExecutionResult<T> RejectFromStatus<T>(SelectorResolveStatus status, string targetCode, string targetDisplayName)
    {
        return status switch
        {
            SelectorResolveStatus.Ambiguous => Rejected<T>($"{targetCode}Ambiguous", $"The {targetDisplayName} selector matched multiple results.", RequiredAction.ResolveTargetAgain),
            _ => Rejected<T>($"{targetCode}NotFound", $"The {targetDisplayName} selector did not match any result.", RequiredAction.ResolveTargetAgain),
        };
    }

    public static PluginExecutionResult<T> Rejected<T>(string code, string message, RequiredAction? requiredAction = null)
    {
        return PluginExecutionResult<T>.Rejected(new PluginExecutionError
        {
            Code = code,
            Message = message,
        }, requiredAction);
    }

    public static PluginExecutionResult<T> RejectProjectStructureFailure<T>(string message)
    {
        return Rejected<T>("ProjectStructureUnavailable", message, RequiredAction.Retry);
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

        return new SymbolSelector
        {
            Location = new LocationSelector
            {
                Span = new TextSpanSelector
                {
                    Document = CreateDocumentSelector(resolvedLocation.Document),
                    Start = resolvedLocation.Span.Start,
                    Length = resolvedLocation.Span.Length,
                },
            },
        };
    }

    public static LocationSelector? CreateLocationSelector(ResolvedLocation? resolvedLocation)
    {
        if (resolvedLocation?.Document is null || resolvedLocation.Span is null)
        {
            return null;
        }

        return new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = CreateDocumentSelector(resolvedLocation.Document),
                Start = resolvedLocation.Span.Start,
                Length = resolvedLocation.Span.Length,
            },
        };
    }

    private static DocumentSelector CreateDocumentSelector(DocumentReference document)
    {
        return !string.IsNullOrWhiteSpace(document.DocumentId)
            ? new DocumentSelector
            {
                DocumentId = document.DocumentId,
            }
            : new DocumentSelector
            {
                Path = document.Path,
            };
    }
}
