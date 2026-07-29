namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

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
        LocationSelector selector,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var resolution = await context.WorkspaceResolver.ResolveLocationAsync(selector, cancellationToken);
        if (!resolution.IsResolved)
        {
            var rejection = CodeActionExecutionResultFactory.RejectFromStatus<TResponse>(
                resolution.Status,
                "Location",
                "location");

            return CodeActionToolResolutionResult.Rejected<CodeActionSourceSelection, TResponse>(rejection);
        }

        var document = context.CurrentSolution.GetDocument(resolution.Value.SourceTree);
        if (document is null)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<TResponse>(
                "LocationNotFound",
                "The location selector did not resolve to a source document.",
                RequiredAction.ResolveTargetAgain);

            return CodeActionToolResolutionResult.Rejected<CodeActionSourceSelection, TResponse>(rejection);
        }

        var selection = new CodeActionSourceSelection
        {
            Document = document,
            Span = resolution.Value.SourceSpan,
        };

        return CodeActionToolResolutionResult.Resolved<CodeActionSourceSelection, TResponse>(selection);
    }

    public async ValueTask<CodeActionToolResolutionResult<CodeActionSourceSelection, TResponse>> ResolveDocumentSelectionAsync<TResponse>(
        DocumentSelector selector,
        TextSpanRange? range,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var resolution = context.WorkspaceResolver.ResolveDocument(selector);
        if (!resolution.IsResolved)
        {
            var rejection = CodeActionExecutionResultFactory.RejectFromStatus<TResponse>(
                resolution.Status,
                "Document",
                "document");

            return CodeActionToolResolutionResult.Rejected<CodeActionSourceSelection, TResponse>(rejection);
        }

        var document = resolution.Value;
        var text = await document.GetTextAsync(cancellationToken);
        if (range is not null
            && (range.Start < 0
                || range.Length < 0
                || range.Start > text.Length
                || range.Length > text.Length - range.Start))
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<TResponse>(
                "InvalidRange",
                "Range must identify a valid UTF-16 span within the selected document.");

            return CodeActionToolResolutionResult.Rejected<CodeActionSourceSelection, TResponse>(rejection);
        }

        var span = new TextSpan(0, text.Length);
        if (range is not null)
        {
            span = new TextSpan(range.Start, range.Length);
        }

        var selection = new CodeActionSourceSelection
        {
            Document = document,
            Span = span,
        };

        return CodeActionToolResolutionResult.Resolved<CodeActionSourceSelection, TResponse>(selection);
    }

    public async ValueTask<CodeActionToolResolutionResult<ISymbol, TResponse>> ResolveSymbolAsync<TResponse>(
        SymbolSelector selector,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (selector.Location is not null)
        {
            var snapshotRejection = ValidateSnapshot<TResponse>(context, expectedSnapshot);
            if (snapshotRejection is not null)
            {
                return CodeActionToolResolutionResult.Rejected<ISymbol, TResponse>(snapshotRejection);
            }
        }

        var resolution = await context.WorkspaceResolver.ResolveSymbolAsync(selector, cancellationToken);
        if (!resolution.IsResolved)
        {
            var rejection = CodeActionExecutionResultFactory.RejectFromStatus<TResponse>(
                resolution.Status,
                "Symbol",
                "symbol");

            return CodeActionToolResolutionResult.Rejected<ISymbol, TResponse>(rejection);
        }

        return CodeActionToolResolutionResult.Resolved<ISymbol, TResponse>(resolution.Value);
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
}
