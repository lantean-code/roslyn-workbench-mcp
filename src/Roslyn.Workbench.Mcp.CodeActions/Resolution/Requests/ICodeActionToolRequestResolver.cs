namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Requests;

internal interface ICodeActionToolRequestResolver
{
    CodeActionExecutionResult<TResponse>? ValidateSnapshot<TResponse>(
        ICodeActionExecutionContext context,
        SnapshotPrecondition? expectedSnapshot);

    ValueTask<CodeActionToolResolutionResult<CodeActionSourceSelection, TResponse>> ResolveLocationAsync<TResponse>(
        LocationSelector? selector,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

    ValueTask<CodeActionToolResolutionResult<ISymbol, TResponse>> ResolveSymbolAsync<TResponse>(
        SymbolSelector? selector,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

    CodeActionScopeResolution ResolveScope(
        ScopeSelector scope,
        ICodeActionExecutionContext context);

    LocationSelector? CreateLocationSelector(ResolvedLocation? resolvedLocation);
}
