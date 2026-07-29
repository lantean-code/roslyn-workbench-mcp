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
    private readonly IWorkspaceMutationCandidateProcessor _candidateProcessor;
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
        IWorkspaceMutationCandidateProcessor candidateProcessor,
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
        _candidateProcessor = candidateProcessor;
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

        var processingResult = await _candidateProcessor.ProcessAsync(
            context.CurrentSolution,
            application.CandidateSolution,
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

        return CreatePreparedReferenceResult(
            request,
            resolution.Reference,
            changedDocuments,
            context.WorkspaceResolver,
            cancellationToken);
    }

    private CodeActionExecutionResult<PrepareFixAllData> CreatePreparedReferenceResult(
        PrepareFixAllRequest request,
        CodeActionReference reference,
        IReadOnlyList<Document> changedDocuments,
        IWorkspaceResolver workspaceResolver,
        CancellationToken cancellationToken)
    {
        var preparedRecipe = reference.Recipe with
        {
            PreparedFixAllScope = request.Scope,
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

    private static CodeActionExecutionResult<PrepareFixAllData>? ValidateResolution(
        DiscoveredCodeAction action,
        CodeActionReference reference,
        CodeActionFixAllScope scope)
    {
        if (reference.Recipe.PreparedFixAllScope is not null)
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
