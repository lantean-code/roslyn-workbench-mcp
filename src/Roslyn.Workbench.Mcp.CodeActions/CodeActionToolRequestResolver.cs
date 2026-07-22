namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionToolRequestResolver : ICodeActionToolRequestResolver
{
    private readonly ICodeActionScopeResolver _scopeResolver;

    public CodeActionToolRequestResolver(ICodeActionScopeResolver scopeResolver)
    {
        _scopeResolver = scopeResolver;
    }

    public CodeActionExecutionResult<TResponse>? ValidateSnapshot<TResponse>(
        ICodeActionExecutionContext context,
        SnapshotPrecondition? expectedSnapshot)
    {
        return CodeActionExecutionResultFactory.ValidateSnapshot<TResponse>(
            context.WorkspaceResolver,
            expectedSnapshot);
    }

    public async ValueTask<CodeActionToolResolutionResult<CodeActionSourceSelection, TResponse>> ResolveLocationAsync<TResponse>(
        LocationSelector? selector,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (selector is null)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<TResponse>(
                "InvalidRequest",
                "A location selector is required.");

            return CodeActionToolResolutionResult<CodeActionSourceSelection, TResponse>.Rejected(rejection);
        }

        var resolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!resolution.IsResolved)
        {
            var rejection = CodeActionExecutionResultFactory.RejectFromStatus<TResponse>(
                resolution.Status,
                "Location",
                "location");

            return CodeActionToolResolutionResult<CodeActionSourceSelection, TResponse>.Rejected(rejection);
        }

        var document = context.CurrentSolution.GetDocument(resolution.Value.SourceTree);
        if (document is null)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<TResponse>(
                "LocationNotFound",
                "The location selector did not resolve to a source document.",
                RequiredAction.ResolveTargetAgain);

            return CodeActionToolResolutionResult<CodeActionSourceSelection, TResponse>.Rejected(rejection);
        }

        var selection = new CodeActionSourceSelection
        {
            Document = document,
            Span = resolution.Value.SourceSpan,
        };

        return CodeActionToolResolutionResult<CodeActionSourceSelection, TResponse>.Resolved(selection);
    }

    public async ValueTask<CodeActionToolResolutionResult<ISymbol, TResponse>> ResolveSymbolAsync<TResponse>(
        SymbolSelector? selector,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (selector?.Location is not null)
        {
            var snapshotRejection = ValidateSnapshot<TResponse>(context, expectedSnapshot);
            if (snapshotRejection is not null)
            {
                return CodeActionToolResolutionResult<ISymbol, TResponse>.Rejected(snapshotRejection);
            }
        }

        if (selector is null)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<TResponse>(
                "InvalidRequest",
                "A symbol selector is required.");

            return CodeActionToolResolutionResult<ISymbol, TResponse>.Rejected(rejection);
        }

        var resolution = await context.WorkspaceResolver.ResolveSymbolAsync(selector, cancellationToken);
        if (!resolution.IsResolved)
        {
            var rejection = CodeActionExecutionResultFactory.RejectFromStatus<TResponse>(
                resolution.Status,
                "Symbol",
                "symbol");

            return CodeActionToolResolutionResult<ISymbol, TResponse>.Rejected(rejection);
        }

        return CodeActionToolResolutionResult<ISymbol, TResponse>.Resolved(resolution.Value);
    }

    public CodeActionScopeResolution ResolveScope(
        ScopeSelector scope,
        ICodeActionExecutionContext context)
    {
        return _scopeResolver.Resolve(
            scope,
            context.CurrentSolution,
            context.WorkspaceResolver);
    }

    public LocationSelector? CreateLocationSelector(ResolvedLocation? resolvedLocation)
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
