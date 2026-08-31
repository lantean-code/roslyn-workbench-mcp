namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

/// <summary>
/// Validates Code Action request preconditions and resolves request selectors into Roslyn objects.
/// </summary>
internal sealed class CodeActionToolRequestResolver : ICodeActionToolRequestResolver
{
    private readonly ICodeActionScopeResolver _scopeResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionToolRequestResolver"/> class.
    /// </summary>
    /// <param name="scopeResolver">The resolver used to expand workspace scopes.</param>
    public CodeActionToolRequestResolver(ICodeActionScopeResolver scopeResolver)
    {
        _scopeResolver = scopeResolver;
    }

    /// <summary>
    /// Validates an optional snapshot precondition against the execution context.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <returns>A rejection when the precondition is not satisfied; otherwise, <see langword="null"/>.</returns>
    public CodeActionExecutionResult<TResponse>? ValidateSnapshot<TResponse>(
        ICodeActionExecutionContext context,
        SnapshotPrecondition? expectedSnapshot)
    {
        return CodeActionExecutionResultFactory.ValidateSnapshot<TResponse>(
            context.WorkspaceResolver,
            expectedSnapshot);
    }

    /// <summary>
    /// Resolves a location selector to a document and source span.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="selector">The selector that identifies the requested workspace scope.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the resolved location.</returns>
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

    /// <summary>
    /// Resolves a document selector and optional range to a source selection.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="selector">The selector that identifies the requested workspace scope.</param>
    /// <param name="range">The optional source range used to select a document span.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the resolved document selection.</returns>
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

    /// <summary>
    /// Resolves a symbol selector after validating any required snapshot precondition.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="selector">The selector that identifies the requested workspace scope.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the resolved symbol.</returns>
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

    /// <summary>
    /// Expands a workspace scope into the documents and projects eligible for execution.
    /// </summary>
    /// <param name="scope">The workspace scope to which the operation applies.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <returns>The selected documents and projects, or a rejection when the scope is invalid.</returns>
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
