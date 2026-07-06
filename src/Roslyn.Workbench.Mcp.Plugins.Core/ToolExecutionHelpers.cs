using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal static class ToolExecutionHelpers
{
    public static int GetMaxResults(IQueryContext context, ResultLimit? requestLimit)
    {
        return requestLimit?.MaxResults ?? context.EffectiveResultLimit.MaxResults ?? 100;
    }

    public static PluginExecutionResult<T> RejectFromStatus<T>(SelectorResolveStatus status, string targetName)
    {
        return status switch
        {
            SelectorResolveStatus.Ambiguous => Rejected<T>($"{targetName}Ambiguous", $"The {targetName.ToLowerInvariant()} selector matched multiple results.", RequiredAction.ResolveTargetAgain),
            _ => Rejected<T>($"{targetName}NotFound", $"The {targetName.ToLowerInvariant()} selector did not match any result.", RequiredAction.ResolveTargetAgain),
        };
    }

    public static PluginExecutionResult<T> Rejected<T>(string code, string message, RequiredAction? requiredAction = null)
    {
        return PluginExecutionResult<T>.Rejected(new ToolError
        {
            Code = code,
            Message = message,
        }, requiredAction);
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
