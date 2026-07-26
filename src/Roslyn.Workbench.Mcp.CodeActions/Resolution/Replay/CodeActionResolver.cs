using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

internal sealed class CodeActionResolver : ICodeActionResolver
{
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionReferenceStore _referenceStore;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public CodeActionResolver(
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionReferenceStore referenceStore,
        ICodeActionToolRequestResolver requestResolver)
    {
        _discoveryService = discoveryService;
        _diagnosticService = diagnosticService;
        _referenceStore = referenceStore;
        _requestResolver = requestResolver;
    }

    public async ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        Guid actionId,
        SnapshotPrecondition? expectedSnapshot,
        DiscoveredActionKind? expectedKind,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotRejection = _requestResolver.ValidateSnapshot<T>(context, expectedSnapshot);

        if (snapshotRejection is not null)
        {
            return RejectedResolution(snapshotRejection);
        }

        var referenceResolution = ResolveReferenceContext(actionId, expectedKind, context);
        if (!referenceResolution.IsResolved)
        {
            return RejectedResolution(CodeActionExecutionResultFactory.ActionExpired<T>());
        }

        var rediscovery = await RediscoverActionsAsync(referenceResolution.Context, cancellationToken);
        if (!rediscovery.ProviderAvailable)
        {
            return RejectedResolution(
                ActionAmbiguous<T>(),
                CodeActionResolutionFailureKind.ProviderUnavailable);
        }

        var action = SelectUniqueAction(rediscovery.Actions, referenceResolution.Context.Reference.Recipe);
        if (action is null)
        {
            return RejectedResolution(ActionAmbiguous<T>());
        }

        if (!action.Descriptor.IsVisible)
        {
            return RejectedResolution(CodeActionExecutionResultFactory.Rejected<T>(
                "ActionUnavailable",
                "The selected action is not available in this server build.",
                RequiredAction.ResolveTargetAgain));
        }

        return CodeActionResolution.Resolved<T>(
            action,
            referenceResolution.Context.Document,
            referenceResolution.Context.Span,
            referenceResolution.Context.Reference);
    }

    private CodeActionReferenceContextResolution ResolveReferenceContext(
        Guid actionId,
        DiscoveredActionKind? expectedKind,
        ICodeActionExecutionContext context)
    {
        if (!_referenceStore.TryGet(actionId, out var reference)
            || expectedKind is not null && reference.Recipe.Kind != expectedKind.Value
            || !MatchesWorkspace(reference.Recipe, context))
        {
            return CodeActionReferenceContextResolution.Unresolved();
        }

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

        var referenceContext = new CodeActionReferenceContext
        {
            Reference = reference,
            Document = documentResolution.Value,
            Span = new TextSpan(recipe.Start, recipe.Length),
        };

        return CodeActionReferenceContextResolution.Resolved(referenceContext);
    }

    private static bool MatchesWorkspace(
        CodeActionReplayRecipe recipe,
        ICodeActionExecutionContext context)
    {
        return string.Equals(recipe.WorkspaceId, context.WorkspaceIdentity.WorkspaceId, StringComparison.Ordinal)
            && recipe.WorkspaceEpoch == context.WorkspaceIdentity.WorkspaceEpoch
            && recipe.TransactionRevision == context.TransactionRevision;
    }

    private async ValueTask<CodeActionRediscovery> RediscoverActionsAsync(
        CodeActionReferenceContext referenceContext,
        CancellationToken cancellationToken)
    {
        var recipe = referenceContext.Reference.Recipe;
        if (recipe.Kind == DiscoveredActionKind.Refactoring)
        {
            var providers = _discoveryService.GetMatchingRefactoringProviders(recipe.ProviderId);
            if (providers.Count != 1)
            {
                return new CodeActionRediscovery();
            }

            var refactorings = await _discoveryService.DiscoverRefactoringsAsync(
                providers[0],
                referenceContext.Document,
                referenceContext.Span,
                cancellationToken);

            return new CodeActionRediscovery
            {
                ProviderAvailable = true,
                Actions = refactorings,
            };
        }

        var codeFixProviders = _discoveryService.GetMatchingCodeFixProviders(recipe.ProviderId);
        if (codeFixProviders.Count != 1)
        {
            return new CodeActionRediscovery();
        }

        var diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(
            referenceContext.Document,
            referenceContext.Span,
            recipe.DiagnosticIds,
            cancellationToken);

        var codeFixes = await _discoveryService.DiscoverCodeFixesAsync(
            codeFixProviders[0],
            referenceContext.Document,
            diagnostics,
            cancellationToken);

        return new CodeActionRediscovery
        {
            ProviderAvailable = true,
            Actions = codeFixes,
        };
    }

    private static DiscoveredCodeAction? SelectUniqueAction(
        IReadOnlyList<DiscoveredCodeAction> actions,
        CodeActionReplayRecipe recipe)
    {
        DiscoveredCodeAction? matchingAction = null;
        foreach (var action in actions)
        {
            if (!string.Equals(action.Title, recipe.Title, StringComparison.Ordinal)
                || !string.Equals(action.EquivalenceKey, recipe.EquivalenceKey, StringComparison.Ordinal)
                || !action.ActionPath.SequenceEqual(recipe.ActionPath)
                || !action.DiagnosticIds.SequenceEqual(recipe.DiagnosticIds, StringComparer.Ordinal)
                || action.TargetSpan.Start != recipe.Start
                || action.TargetSpan.Length != recipe.Length)
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

    private sealed record CodeActionRediscovery
    {
        public bool ProviderAvailable { get; init; }

        public IReadOnlyList<DiscoveredCodeAction> Actions { get; init; } = [];
    }
}
