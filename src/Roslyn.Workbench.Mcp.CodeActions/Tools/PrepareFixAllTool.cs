using Microsoft.Extensions.Options;
using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class PrepareFixAllTool : CodeActionQueryToolHandler<PrepareFixAllRequest, PrepareFixAllData>
{
    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionEvaluator _evaluator;
    private readonly IFixAllActionFactory _fixAllActionFactory;
    private readonly ICodeActionReferenceStore _referenceStore;
    private readonly ICodeActionResolver _resolver;
    private readonly ICodeActionSolutionChangeCounter _solutionChangeCounter;
    private readonly IWorkspaceMutationCandidateValidator _candidateValidator;
    private readonly ILinkedDocumentChangeMerger _linkedDocumentChangeMerger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _referenceLifetime;

    public PrepareFixAllTool(
        ICodeActionComposition composition,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionEvaluator evaluator,
        IFixAllActionFactory fixAllActionFactory,
        ICodeActionReferenceStore referenceStore,
        ICodeActionResolver resolver,
        ICodeActionSolutionChangeCounter solutionChangeCounter,
        IWorkspaceMutationCandidateValidator candidateValidator,
        ILinkedDocumentChangeMerger linkedDocumentChangeMerger,
        TimeProvider timeProvider,
        IOptions<CodeActionExecutionOptions> options)
    {
        _composition = composition;
        _discoveryService = discoveryService;
        _evaluator = evaluator;
        _fixAllActionFactory = fixAllActionFactory;
        _referenceStore = referenceStore;
        _resolver = resolver;
        _solutionChangeCounter = solutionChangeCounter;
        _candidateValidator = candidateValidator;
        _linkedDocumentChangeMerger = linkedDocumentChangeMerger;
        _timeProvider = timeProvider;
        _referenceLifetime = options.Value.ReferenceLifetime;
    }

    protected override async ValueTask<CodeActionExecutionResult<PrepareFixAllData>> ExecuteCoreAsync(
        PrepareFixAllRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        if (!_composition.Status.IsAvailable)
        {
            return CodeActionsUnavailable<PrepareFixAllData>();
        }

        if (!Enum.IsDefined(request.Scope))
        {
            return Rejected<PrepareFixAllData>(
                "InvalidRequest",
                "Scope must identify a supported Fix All scope.");
        }

        if (request.MaxChanges is < 0 || request.AffectedDocumentsLimit is < 0)
        {
            return Rejected<PrepareFixAllData>(
                "InvalidRequest",
                "MaxChanges and AffectedDocumentsLimit must be zero or greater.");
        }

        var resolution = await _resolver.ResolveActionAsync<PrepareFixAllData>(
            request.ActionId,
            request.ExpectedSnapshot,
            context,
            cancellationToken);

        if (resolution.HasRejection)
        {
            return resolution.Rejection;
        }

        if (resolution.Reference.Recipe.PreparedFixAllScope is not null)
        {
            return Rejected<PrepareFixAllData>(
                "FixAllUnavailable",
                "The selected reference already represents a prepared Fix All operation.");
        }

        if (resolution.Action.Kind != DiscoveredActionKind.CodeFix)
        {
            return Rejected<PrepareFixAllData>("FixAllUnavailable", "The selected action is not a Code Fix.");
        }

        if (!resolution.Action.FixAllScopes.Contains(request.Scope))
        {
            return Rejected<PrepareFixAllData>(
                "FixAllUnavailable",
                "The selected Code Fix does not support the requested Fix All scope.");
        }

        var provider = _discoveryService.FindCodeFixProvider(resolution.Action.ProviderId);
        var fixAllProvider = provider?.GetFixAllProvider();
        if (provider is null || fixAllProvider is null)
        {
            return Rejected<PrepareFixAllData>(
                "FixAllUnavailable",
                "The originating Code Fix no longer exposes Fix All.");
        }

        var creation = await CreateFixAllActionAsync(
            request.Scope,
            provider,
            fixAllProvider,
            resolution.Action,
            resolution.Document,
            cancellationToken);

        if (creation.HasFailure)
        {
            return Rejected<PrepareFixAllData>("FixAllUnavailable", creation.Failure.Message);
        }

        var application = await _evaluator.EvaluateAsync(
            creation.Action,
            context.CurrentSolution,
            cancellationToken);

        if (application.HasFailure)
        {
            return Rejected<PrepareFixAllData>(application.Failure);
        }

        var candidateSolution = application.CandidateSolution;
        var validationError = _candidateValidator.Validate(
            context.CurrentSolution,
            candidateSolution);

        if (validationError is not null)
        {
            return Rejected<PrepareFixAllData>(
                validationError.Code,
                validationError.Message);
        }

        var mergeResult = await _linkedDocumentChangeMerger.MergeAsync(
            context.CurrentSolution,
            candidateSolution,
            cancellationToken);

        if (!mergeResult.IsSucceeded)
        {
            return Rejected<PrepareFixAllData>(
                mergeResult.Error.Code,
                mergeResult.Error.Message);
        }

        candidateSolution = mergeResult.Solution;
        validationError = _candidateValidator.Validate(
            context.CurrentSolution,
            candidateSolution);

        if (validationError is not null)
        {
            return Rejected<PrepareFixAllData>(
                validationError.Code,
                validationError.Message);
        }

        var changedDocuments = await _solutionChangeCounter.GetChangedSourceDocumentsAsync(
            context.CurrentSolution,
            candidateSolution,
            cancellationToken);

        if (changedDocuments.Count > request.EffectiveMaxChanges)
        {
            return Rejected<PrepareFixAllData>(
                "FixAllLimitExceeded",
                $"The Fix All operation would change {changedDocuments.Count} source documents, exceeding the limit of {request.EffectiveMaxChanges}.",
                RequiredAction.NarrowRequest);
        }

        var preparedRecipe = resolution.Reference.Recipe with
        {
            PreparedFixAllScope = request.Scope,
        };

        var affectedDocuments = CreateAffectedDocuments(
            changedDocuments,
            request.EffectiveAffectedDocumentsLimit,
            context.WorkspaceResolver,
            cancellationToken);

        var expiresAt = _timeProvider.GetUtcNow().Add(_referenceLifetime);
        if (!_referenceStore.TryCreate(preparedRecipe, expiresAt, out var preparedReference))
        {
            return Rejected<PrepareFixAllData>(
                "ActionReferenceCapacityExceeded",
                "The prepared Fix All reference could not be stored.");
        }

        var data = new PrepareFixAllData
        {
            ActionId = preparedReference.ActionId,
            Scope = request.Scope,
            AffectedDocuments = affectedDocuments,
        };

        return CodeActionExecutionResult.Success(data);
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

    private static BoundedCollection<DocumentReference> CreateAffectedDocuments(
        IReadOnlyList<Document> changedDocuments,
        int limit,
        IWorkspaceResolver workspaceResolver,
        CancellationToken cancellationToken)
    {
        var affectedDocuments = new List<DocumentReference>(Math.Min(changedDocuments.Count, limit));
        foreach (var document in changedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (affectedDocuments.Count == limit)
            {
                break;
            }

            var reference = workspaceResolver.CreateDocumentReference(document);
            if (reference is not null)
            {
                affectedDocuments.Add(reference);
            }
        }

        return BoundedCollection.CreatePrebounded(affectedDocuments, changedDocuments.Count);
    }
}
