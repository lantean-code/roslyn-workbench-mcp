using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

internal sealed class PreparedFixAllResolver : IPreparedFixAllResolver
{
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly IFixAllActionFactory _fixAllActionFactory;
    private readonly ICodeActionResolver _resolver;

    public PreparedFixAllResolver(
        ICodeActionDiscoveryService discoveryService,
        IFixAllActionFactory fixAllActionFactory,
        ICodeActionResolver resolver)
    {
        _discoveryService = discoveryService;
        _fixAllActionFactory = fixAllActionFactory;
        _resolver = resolver;
    }

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

        var scope = resolution.Reference.Recipe.PreparedFixAllScope;
        if (scope is null
            || resolution.Action.Kind != DiscoveredActionKind.CodeFix
            || !resolution.Action.FixAllScopes.Contains(scope.Value))
        {
            return Unavailable<T>("The prepared Fix All scope is no longer available.");
        }

        var provider = _discoveryService.FindCodeFixProvider(resolution.Action.ProviderId);
        var fixAllProvider = provider?.GetFixAllProvider();
        if (provider is null || fixAllProvider is null)
        {
            return Unavailable<T>("The originating Code Fix no longer exposes Fix All.");
        }

        var creation = await CreateFixAllActionAsync(
            scope.Value,
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
                syntheticDiagnosticId: null,
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
                syntheticDiagnosticId: null,
                cancellationToken);
        }

        return _fixAllActionFactory.CreateSolutionAsync(
            provider,
            fixAllProvider,
            originDocument,
            action.DiagnosticIds,
            action.EquivalenceKey,
            syntheticDiagnosticId: null,
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
