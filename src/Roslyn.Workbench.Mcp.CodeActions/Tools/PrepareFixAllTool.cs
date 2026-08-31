using Microsoft.Extensions.Options;
using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

/// <summary>
/// Evaluates and retains a bounded Fix All operation for later transactional staging.
/// </summary>
internal sealed class PrepareFixAllTool : CodeActionQueryToolHandler<PrepareFixAllRequest, PrepareFixAllData>
{
    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionEvaluator _evaluator;
    private readonly IFixAllActionFactory _fixAllActionFactory;
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionReferenceStore _referenceStore;
    private readonly ICodeActionResolver _resolver;
    private readonly ICodeActionSolutionChangeCounter _solutionChangeCounter;
    private readonly IWorkspaceMutationCandidateProcessor _candidateProcessor;
    private readonly IWorkspaceMutationCandidateIdentityService _candidateIdentityService;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _referenceLifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrepareFixAllTool"/> class.
    /// </summary>
    /// <param name="composition">The composed service set being inspected.</param>
    /// <param name="evaluator">The component that evaluates a Code Action into a candidate solution.</param>
    /// <param name="fixAllActionFactory">The factory used to create the required fix all action.</param>
    /// <param name="providerCatalog">The catalogue used to locate Code Action providers.</param>
    /// <param name="referenceStore">The store that retains replayable references.</param>
    /// <param name="resolver">The resolver that rehydrates the originating Code Action reference.</param>
    /// <param name="solutionChangeCounter">The component that counts source documents changed by a Code Action.</param>
    /// <param name="candidateProcessor">The processor that normalizes and validates a candidate solution before staging.</param>
    /// <param name="candidateIdentityService">The service that creates and validates candidate solution identities.</param>
    /// <param name="timeProvider">The time source used for expiry and timestamp calculations.</param>
    /// <param name="options">The options that configure the operation.</param>
    public PrepareFixAllTool(
        ICodeActionComposition composition,
        ICodeActionEvaluator evaluator,
        IFixAllActionFactory fixAllActionFactory,
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionReferenceStore referenceStore,
        ICodeActionResolver resolver,
        ICodeActionSolutionChangeCounter solutionChangeCounter,
        IWorkspaceMutationCandidateProcessor candidateProcessor,
        IWorkspaceMutationCandidateIdentityService candidateIdentityService,
        TimeProvider timeProvider,
        IOptions<CodeActionExecutionOptions> options)
    {
        _composition = composition;
        _evaluator = evaluator;
        _fixAllActionFactory = fixAllActionFactory;
        _providerCatalog = providerCatalog;
        _referenceStore = referenceStore;
        _resolver = resolver;
        _solutionChangeCounter = solutionChangeCounter;
        _candidateProcessor = candidateProcessor;
        _candidateIdentityService = candidateIdentityService;
        _timeProvider = timeProvider;
        _referenceLifetime = options.Value.ReferenceLifetime;
    }

    /// <inheritdoc/>
    protected override async ValueTask<CodeActionExecutionResult<PrepareFixAllData>> ExecuteCoreAsync(
        PrepareFixAllRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        if (!_composition.Status.IsAvailable)
        {
            return CodeActionsUnavailable<PrepareFixAllData>();
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

        var resolutionRejection = ValidateResolution(
            resolution.Action,
            resolution.Reference,
            request.Scope);
        if (resolutionRejection is not null)
        {
            return resolutionRejection;
        }

        var provider = _providerCatalog.FindCodeFixProvider(resolution.Action.ProviderId);
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

        var processingResult = await _candidateProcessor.ProcessAsync(
            context.CurrentSolution,
            application.CandidateSolution,
            context.WorkspaceIdentity.WorkspaceRoot,
            cancellationToken);

        if (!processingResult.IsSucceeded)
        {
            return Rejected<PrepareFixAllData>(
                processingResult.Error.Code,
                processingResult.Error.Message);
        }

        var changedDocuments = await _solutionChangeCounter.GetChangedSourceDocumentsAsync(
            context.CurrentSolution,
            processingResult.Solution,
            cancellationToken);

        if (changedDocuments.Count > request.EffectiveMaxChanges)
        {
            return Rejected<PrepareFixAllData>(
                "FixAllLimitExceeded",
                $"The Fix All operation would change {changedDocuments.Count} source documents, exceeding the limit of {request.EffectiveMaxChanges}.",
                RequiredAction.NarrowRequest);
        }

        var candidateIdentity = await _candidateIdentityService.CreateAsync(
            context.CurrentSolution,
            processingResult.Solution,
            cancellationToken);

        return CreatePreparedReferenceResult(
            request,
            resolution.Reference,
            changedDocuments,
            candidateIdentity,
            context.WorkspaceResolver,
            cancellationToken);
    }

    private CodeActionExecutionResult<PrepareFixAllData> CreatePreparedReferenceResult(
        PrepareFixAllRequest request,
        CodeActionReference reference,
        IReadOnlyList<Document> changedDocuments,
        WorkspaceMutationCandidateIdentity candidateIdentity,
        IWorkspaceResolver workspaceResolver,
        CancellationToken cancellationToken)
    {
        var candidatePrecondition = new WorkspaceMutationCandidatePrecondition
        {
            ExpectedIdentity = candidateIdentity,
            MaximumChangedDocuments = request.EffectiveMaxChanges,
        };

        var preparedFixAll = new PreparedFixAllReplayData
        {
            Scope = request.Scope,
            CandidatePrecondition = candidatePrecondition,
        };

        var preparedRecipe = reference.Recipe with
        {
            PreparedFixAll = preparedFixAll,
        };

        var affectedDocuments = CreateAffectedDocuments(
            changedDocuments,
            request.EffectiveAffectedDocumentsLimit,
            workspaceResolver,
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

    private static CodeActionExecutionResult<PrepareFixAllData>? ValidateResolution(
        DiscoveredCodeAction action,
        CodeActionReference reference,
        CodeActionFixAllScope scope)
    {
        if (reference.Recipe.PreparedFixAll is not null)
        {
            return Rejected<PrepareFixAllData>(
                "FixAllUnavailable",
                "The selected reference already represents a prepared Fix All operation.");
        }

        if (action.Kind != DiscoveredActionKind.CodeFix)
        {
            return Rejected<PrepareFixAllData>(
                "FixAllUnavailable",
                "The selected action is not a Code Fix.");
        }

        if (!action.FixAllScopes.Contains(scope))
        {
            return Rejected<PrepareFixAllData>(
                "FixAllUnavailable",
                "The selected Code Fix does not support the requested Fix All scope.");
        }

        return null;
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
