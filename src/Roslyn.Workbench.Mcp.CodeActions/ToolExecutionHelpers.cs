namespace Roslyn.Workbench.Mcp.CodeActions;

internal static class ToolExecutionHelpers
{
    public static async ValueTask<CodeActionToolResolutionResult<ISymbol, TResponse>> ResolveSymbolAsync<TResponse>(
        SymbolSelector? selector,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var snapshotRejection = selector?.Location is not null
            ? ValidateSnapshot<TResponse>(context, expectedSnapshot)
            : null;
        if (snapshotRejection is not null)
        {
            return new CodeActionToolResolutionResult<ISymbol, TResponse>
            {
                Rejection = snapshotRejection,
            };
        }

        if (selector is null)
        {
            return new CodeActionToolResolutionResult<ISymbol, TResponse>
            {
                Rejection = Rejected<TResponse>("InvalidRequest", "A symbol selector is required."),
            };
        }

        var resolution = await context.WorkspaceResolver
            .ResolveSymbolAsync(selector, cancellationToken)
            .ConfigureAwait(false);
        return resolution.Status == SelectorResolveStatus.Resolved
            ? new CodeActionToolResolutionResult<ISymbol, TResponse> { Value = resolution.Value! }
            : new CodeActionToolResolutionResult<ISymbol, TResponse>
            {
                Rejection = RejectFromStatus<TResponse>(resolution.Status, "Symbol"),
            };
    }

    public static CodeActionExecutionResult<TResponse>? ValidateSnapshot<TResponse>(
        ICodeActionExecutionContext context,
        SnapshotPrecondition? expectedSnapshot)
    {
        var result = context.WorkspaceResolver.ValidateSnapshot(expectedSnapshot);
        return result.Kind == SnapshotMatchKind.Matched
            ? null
            : CodeActionExecutionResult<TResponse>.Conflict(
                new CodeActionExecutionError
                {
                    Code = "SnapshotMismatch",
                    Message = "The request snapshot does not match the current workspace snapshot.",
                },
                RequiredAction.ResolveTargetAgain);
    }

    public static CodeActionExecutionResult<T> RejectFromStatus<T>(SelectorResolveStatus status, string targetName)
    {
        return status switch
        {
            SelectorResolveStatus.Ambiguous => Rejected<T>($"{targetName}Ambiguous", $"The {targetName.ToLowerInvariant()} selector matched multiple results.", RequiredAction.ResolveTargetAgain),
            _ => Rejected<T>($"{targetName}NotFound", $"The {targetName.ToLowerInvariant()} selector did not match any result.", RequiredAction.ResolveTargetAgain),
        };
    }

    public static CodeActionExecutionResult<T> Rejected<T>(string code, string message, RequiredAction? requiredAction = null)
    {
        return CodeActionExecutionResult<T>.Rejected(new CodeActionExecutionError
        {
            Code = code,
            Message = message,
        }, requiredAction);
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
