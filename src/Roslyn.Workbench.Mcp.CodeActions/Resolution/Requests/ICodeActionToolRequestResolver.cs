namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

/// <summary>
/// Validates Code Action request preconditions and resolves request selectors into Roslyn objects.
/// </summary>
internal interface ICodeActionToolRequestResolver
{
    /// <summary>
    /// Validates an optional snapshot precondition against the execution context.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <returns>A rejection when the precondition is not satisfied; otherwise, <see langword="null"/>.</returns>
    CodeActionExecutionResult<TResponse>? ValidateSnapshot<TResponse>(
        ICodeActionExecutionContext context,
        SnapshotPrecondition? expectedSnapshot);

    /// <summary>
    /// Resolves a location selector to a document and source span.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="selector">The selector that identifies the requested workspace scope.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the resolved location.</returns>
    ValueTask<CodeActionToolResolutionResult<CodeActionSourceSelection, TResponse>> ResolveLocationAsync<TResponse>(
        LocationSelector selector,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a document selector and optional range to a source selection.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="selector">The selector that identifies the requested workspace scope.</param>
    /// <param name="range">The optional source range used to select a document span.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the resolved document selection.</returns>
    ValueTask<CodeActionToolResolutionResult<CodeActionSourceSelection, TResponse>> ResolveDocumentSelectionAsync<TResponse>(
        DocumentSelector selector,
        TextSpanRange? range,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a symbol selector after validating any required snapshot precondition.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="selector">The selector that identifies the requested workspace scope.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the resolved symbol.</returns>
    ValueTask<CodeActionToolResolutionResult<ISymbol, TResponse>> ResolveSymbolAsync<TResponse>(
        SymbolSelector selector,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Expands a workspace scope into the documents and projects eligible for execution.
    /// </summary>
    /// <param name="scope">The workspace scope to which the operation applies.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <returns>The selected documents and projects, or a rejection when the scope is invalid.</returns>
    CodeActionScopeResolution ResolveScope(
        ScopeSelector scope,
        ICodeActionExecutionContext context);
}
