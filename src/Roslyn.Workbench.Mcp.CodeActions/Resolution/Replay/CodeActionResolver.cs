using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

internal sealed class CodeActionResolver : ICodeActionResolver
{
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionReferenceStore _referenceStore;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public CodeActionResolver(
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionReferenceStore referenceStore,
        ICodeActionToolRequestResolver requestResolver)
    {
        _discoveryService = discoveryService;
        _diagnosticService = diagnosticService;
        _providerCatalog = providerCatalog;
        _referenceStore = referenceStore;
        _requestResolver = requestResolver;
    }

    public async ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        Guid actionId,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotRejection = _requestResolver.ValidateSnapshot<T>(context, expectedSnapshot);

        if (snapshotRejection is not null)
        {
            return RejectedResolution(snapshotRejection);
        }

        if (!_referenceStore.TryGet(actionId, out var reference))
        {
            return RejectedResolution(
                CodeActionExecutionResultFactory.ActionExpired<T>(),
                CodeActionResolutionFailureKind.InvalidReference);
        }

        if (!MatchesWorkspaceInstance(reference.Recipe.SnapshotIdentity, context.SnapshotIdentity))
        {
            return RejectedResolution(
                CodeActionExecutionResultFactory.ActionExpired<T>(),
                CodeActionResolutionFailureKind.InvalidReference);
        }

        if (reference.Recipe.SnapshotIdentity != context.SnapshotIdentity)
        {
            return RejectedResolution(CodeActionExecutionResultFactory.SnapshotMismatch<T>());
        }

        var referenceResolution = ResolveReferenceContext(reference, context);
        if (!referenceResolution.IsResolved)
        {
            return RejectedResolution(
                CodeActionExecutionResultFactory.ActionExpired<T>(),
                CodeActionResolutionFailureKind.InvalidReference);
        }

        var rediscovery = await RediscoverActionsAsync(referenceResolution.Context, cancellationToken);
        if (!rediscovery.IsSuccessful)
        {
            if (rediscovery.Status == CodeActionRediscoveryStatus.ProviderUnavailable)
            {
                return RejectedResolution(
                    ActionAmbiguous<T>(),
                    CodeActionResolutionFailureKind.ProviderUnavailable);
            }

            return RejectedResolution(
                ProviderFailed<T>());
        }

        var action = SelectUniqueAction(rediscovery.Actions, referenceResolution.Context.Reference.Recipe);
        if (action is null)
        {
            return RejectedResolution(
                ActionAmbiguous<T>(),
                CodeActionResolutionFailureKind.InvalidReference);
        }

        return CodeActionResolution.Resolved<T>(
            action,
            referenceResolution.Context.Document,
            referenceResolution.Context.Span,
            referenceResolution.Context.Reference);
    }

    private static CodeActionReferenceContextResolution ResolveReferenceContext(
        CodeActionReference reference,
        ICodeActionExecutionContext context)
    {
        var recipe = reference.Recipe;
        ProjectSelector? project = null;
        if (!string.IsNullOrWhiteSpace(recipe.ProjectId))
        {
            project = new ProjectSelector
            {
                ProjectId = recipe.ProjectId,
            };
        }

        var documentSelector = new DocumentSelector
        {
            Path = recipe.DocumentPath,
            Project = project,
        };

        var documentResolution = context.WorkspaceResolver.ResolveDocument(documentSelector);

        if (documentResolution.Status != SelectorResolveStatus.Resolved || documentResolution.Value is null)
        {
            return CodeActionReferenceContextResolution.Unresolved();
        }

        var span = new TextSpan(recipe.Start, recipe.Length);
        var referenceContext = new CodeActionReferenceContext
        {
            Reference = reference,
            Document = documentResolution.Value,
            Span = span,
        };

        return CodeActionReferenceContextResolution.Resolved(referenceContext);
    }

    private static bool MatchesWorkspaceInstance(
        WorkspaceSnapshotIdentity referenceIdentity,
        WorkspaceSnapshotIdentity contextIdentity)
    {
        return referenceIdentity.WorkspaceId == contextIdentity.WorkspaceId
            && referenceIdentity.WorkspaceEpoch == contextIdentity.WorkspaceEpoch;
    }

    private async ValueTask<CodeActionRediscovery> RediscoverActionsAsync(
        CodeActionReferenceContext referenceContext,
        CancellationToken cancellationToken)
    {
        var recipe = referenceContext.Reference.Recipe;
        if (recipe.Kind == DiscoveredActionKind.Refactoring)
        {
            var provider = _providerCatalog.FindRefactoringProvider(recipe.ProviderId);
            if (provider is null)
            {
                return CodeActionRediscovery.ProviderUnavailable();
            }

            var refactorings = await _discoveryService.RediscoverRefactoringsAsync(
                provider,
                referenceContext.Document,
                referenceContext.Span,
                cancellationToken);

            if (!refactorings.IsSuccessful)
            {
                return CodeActionRediscovery.ProviderFailed();
            }

            return CodeActionRediscovery.Succeeded(refactorings.Value);
        }

        var codeFixProvider = _providerCatalog.FindCodeFixProvider(recipe.ProviderId);
        if (codeFixProvider is null)
        {
            return CodeActionRediscovery.ProviderUnavailable();
        }

        var inspection = _discoveryService.ReadCodeFixProviderMetadata(codeFixProvider, cancellationToken);
        if (!inspection.IsSuccessful)
        {
            return CodeActionRediscovery.ProviderFailed();
        }

        var diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(
            referenceContext.Document,
            referenceContext.Span,
            recipe.DiagnosticIds,
            cancellationToken);

        var codeFixes = await _discoveryService.RediscoverCodeFixesAsync(
            inspection.Value,
            referenceContext.Document,
            diagnostics,
            cancellationToken);

        if (!codeFixes.IsSuccessful)
        {
            return CodeActionRediscovery.ProviderFailed();
        }

        return CodeActionRediscovery.Succeeded(codeFixes.Value);
    }

    private static DiscoveredCodeAction? SelectUniqueAction(
        IReadOnlyList<DiscoveredCodeAction> actions,
        CodeActionReplayRecipe recipe)
    {
        DiscoveredCodeAction? matchingAction = null;
        foreach (var action in actions)
        {
            if (action.Kind != recipe.Kind
                || !string.Equals(action.ProviderId, recipe.ProviderId, StringComparison.Ordinal)
                || !string.Equals(action.EquivalenceKey, recipe.EquivalenceKey, StringComparison.Ordinal)
                || !action.ActionPath.SequenceEqual(recipe.ActionPath)
                || !action.DiagnosticIds.SequenceEqual(recipe.DiagnosticIds, StringComparer.Ordinal)
                || !action.Diagnostics.SequenceEqual(recipe.Diagnostics)
                || action.TargetSpan.Start != recipe.Start
                || action.TargetSpan.Length != recipe.Length
                || !string.Equals(action.Title, recipe.Title, StringComparison.Ordinal))
            {
                continue;
            }

            if (matchingAction is not null)
            {
                return null;
            }

            matchingAction = action;
        }

        return matchingAction;
    }

    private static CodeActionResolution<T> RejectedResolution<T>(
        CodeActionExecutionResult<T> rejection,
        CodeActionResolutionFailureKind failureKind = CodeActionResolutionFailureKind.None)
    {
        return CodeActionResolution.Rejected(rejection, failureKind);
    }

    private static CodeActionExecutionResult<T> ActionAmbiguous<T>()
    {
        return CodeActionExecutionResultFactory.Rejected<T>(
            "ActionAmbiguous",
            "The requested action could not be reproduced uniquely.",
            RequiredAction.ResolveTargetAgain);
    }

    private static CodeActionExecutionResult<T> ProviderFailed<T>()
    {
        return CodeActionExecutionResultFactory.Rejected<T>(
            "ActionUnavailable",
            "The selected action could not be reproduced because its provider failed. Retry the same request.",
            RequiredAction.Retry);
    }

    private sealed record CodeActionReferenceContextResolution
    {
        public CodeActionReferenceContext? Context { get; }

        [MemberNotNullWhen(true, nameof(Context))]
        public bool IsResolved => Context is not null;

        private CodeActionReferenceContextResolution(CodeActionReferenceContext? context)
        {
            Context = context;
        }

        public static CodeActionReferenceContextResolution Resolved(CodeActionReferenceContext context)
        {
            return new CodeActionReferenceContextResolution(context);
        }

        public static CodeActionReferenceContextResolution Unresolved()
        {
            return new CodeActionReferenceContextResolution(context: null);
        }
    }

    private sealed record CodeActionReferenceContext
    {
        public required CodeActionReference Reference { get; init; }

        public required Document Document { get; init; }

        public required TextSpan Span { get; init; }
    }

    private sealed class CodeActionRediscovery
    {
        public CodeActionRediscoveryStatus Status { get; }

        public IReadOnlyList<DiscoveredCodeAction>? Actions { get; }

        [MemberNotNullWhen(true, nameof(Actions))]
        public bool IsSuccessful => Status == CodeActionRediscoveryStatus.Succeeded;

        private CodeActionRediscovery(
            CodeActionRediscoveryStatus status,
            IReadOnlyList<DiscoveredCodeAction>? actions)
        {
            Status = status;
            Actions = actions;
        }

        public static CodeActionRediscovery Succeeded(IReadOnlyList<DiscoveredCodeAction> actions)
        {
            return new CodeActionRediscovery(CodeActionRediscoveryStatus.Succeeded, actions);
        }

        public static CodeActionRediscovery ProviderUnavailable()
        {
            return new CodeActionRediscovery(CodeActionRediscoveryStatus.ProviderUnavailable, actions: null);
        }

        public static CodeActionRediscovery ProviderFailed()
        {
            return new CodeActionRediscovery(CodeActionRediscoveryStatus.ProviderFailed, actions: null);
        }
    }

    private enum CodeActionRediscoveryStatus
    {
        Succeeded,
        ProviderUnavailable,
        ProviderFailed,
    }
}
