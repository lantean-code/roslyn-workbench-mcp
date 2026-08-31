using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

/// <summary>
/// Rediscovers a source Code Fix and recreates the Fix All action selected during preparation.
/// </summary>
internal sealed class PreparedFixAllResolver : IPreparedFixAllResolver
{
    private readonly IFixAllActionFactory _fixAllActionFactory;
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreparedFixAllResolver"/> class.
    /// </summary>
    /// <param name="fixAllActionFactory">The factory used to create the required fix all action.</param>
    /// <param name="providerCatalog">The catalogue used to locate Code Action providers.</param>
    /// <param name="resolver">The resolver used to rediscover the source Code Fix.</param>
    public PreparedFixAllResolver(
        IFixAllActionFactory fixAllActionFactory,
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionResolver resolver)
    {
        _fixAllActionFactory = fixAllActionFactory;
        _providerCatalog = providerCatalog;
        _resolver = resolver;
    }

    /// <summary>
    /// Rediscover the source Code Fix and recreate its prepared Fix All action.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actionId">The action identifier.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the recreated Fix All action and source context, or a rejection.</returns>
    public async ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        Guid actionId,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var resolution = await _resolver.ResolveActionAsync<T>(
            actionId,
            expectedSnapshot,
            context,
            cancellationToken);

        if (resolution.HasRejection)
        {
            return resolution;
        }

        var preparedFixAll = resolution.Reference.Recipe.PreparedFixAll;
        if (preparedFixAll is null
            || resolution.Action.Kind != DiscoveredActionKind.CodeFix
            || !resolution.Action.FixAllScopes.Contains(preparedFixAll.Scope))
        {
            return Unavailable<T>("The prepared Fix All scope is no longer available.");
        }

        var provider = _providerCatalog.FindCodeFixProvider(resolution.Action.ProviderId);
        var fixAllProvider = provider?.GetFixAllProvider();
        if (provider is null || fixAllProvider is null)
        {
            return Unavailable<T>("The originating Code Fix no longer exposes Fix All.");
        }

        var creation = await CreateFixAllActionAsync(
            preparedFixAll.Scope,
            provider,
            fixAllProvider,
            resolution.Action,
            resolution.Document,
            cancellationToken);

        if (creation.HasFailure)
        {
            return Unavailable<T>(creation.Failure.Message);
        }

        var preparedAction = resolution.Action with
        {
            Action = creation.Action,
            Title = $"Fix all: {resolution.Action.Title}",
        };

        return CodeActionResolution.Resolved<T>(
            preparedAction,
            resolution.Document,
            resolution.Span,
            resolution.Reference);
    }

    private Task<FixAllActionCreationResult> CreateFixAllActionAsync(
        CodeActionFixAllScope scope,
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        DiscoveredCodeAction action,
        Document originDocument,
        CancellationToken cancellationToken)
    {
        if (scope == CodeActionFixAllScope.Document)
        {
            return _fixAllActionFactory.CreateDocumentAsync(
                provider,
                fixAllProvider,
                originDocument,
                action.DiagnosticIds,
                action.EquivalenceKey,
                cancellationToken);
        }

        if (scope == CodeActionFixAllScope.Project)
        {
            return _fixAllActionFactory.CreateProjectAsync(
                provider,
                fixAllProvider,
                originDocument.Project,
                action.DiagnosticIds,
                action.EquivalenceKey,
                cancellationToken);
        }

        return _fixAllActionFactory.CreateSolutionAsync(
            provider,
            fixAllProvider,
            originDocument,
            action.DiagnosticIds,
            action.EquivalenceKey,
            cancellationToken);
    }

    private static CodeActionResolution<T> Unavailable<T>(string message)
    {
        var rejection = Rejected<T>("FixAllUnavailable", message);
        return CodeActionResolution.Rejected(
            rejection,
            CodeActionResolutionFailureKind.InvalidReference);
    }
}
