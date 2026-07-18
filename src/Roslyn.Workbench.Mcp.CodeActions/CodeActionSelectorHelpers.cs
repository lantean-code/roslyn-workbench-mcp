namespace Roslyn.Workbench.Mcp.CodeActions;

internal static class CodeActionSelectorHelpers
{
    public static async ValueTask<CodeActionToolResolutionResult<ISymbol, TResponse>> ResolveSymbolAsync<TResponse>(
        SymbolSelector? selector,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var snapshotRejection = selector?.Location is not null
            ? CodeActionExecutionResultFactory.ValidateSnapshot<TResponse>(context.WorkspaceResolver, expectedSnapshot)
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
                Rejection = CodeActionExecutionResultFactory.Rejected<TResponse>("InvalidRequest", "A symbol selector is required."),
            };
        }

        var resolution = await context.WorkspaceResolver
            .ResolveSymbolAsync(selector, cancellationToken)
            .ConfigureAwait(false);
        return resolution.IsResolved
            ? new CodeActionToolResolutionResult<ISymbol, TResponse> { Value = resolution.Value }
            : new CodeActionToolResolutionResult<ISymbol, TResponse>
            {
                Rejection = CodeActionExecutionResultFactory.RejectFromStatus<TResponse>(resolution.Status, "Symbol", "symbol"),
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
