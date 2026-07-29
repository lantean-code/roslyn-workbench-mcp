using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed class CodeActionFixAllStager : ICodeActionFixAllStager
{
    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionResolver _resolver;
    private readonly ICodeActionEvaluator _evaluator;
    private readonly IFixAllActionFactory _fixAllActionFactory;
    private readonly ICodeActionToolRequestResolver _requestResolver;
    private readonly ICodeActionSolutionChangeCounter _solutionChangeCounter;

    public CodeActionFixAllStager(
        ICodeActionComposition composition,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionResolver resolver,
        ICodeActionEvaluator evaluator,
        IFixAllActionFactory fixAllActionFactory,
        ICodeActionToolRequestResolver requestResolver,
        ICodeActionSolutionChangeCounter solutionChangeCounter)
    {
        _composition = composition;
        _discoveryService = discoveryService;
        _resolver = resolver;
        _evaluator = evaluator;
        _fixAllActionFactory = fixAllActionFactory;
        _requestResolver = requestResolver;
        _solutionChangeCounter = solutionChangeCounter;
    }

    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageFixAllAsync(
        StageFixAllRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtimeRejection = RejectedIfUnavailable();
        if (runtimeRejection is not null)
        {
            return runtimeRejection;
        }

        var resolution = await ResolveActionAsync(request, context, cancellationToken);
        if (resolution.HasRejection)
        {
            return MapResolutionRejection(resolution.FailureKind, resolution.Rejection);
        }

        if (resolution.Action.Kind != DiscoveredActionKind.CodeFix)
        {
            return FixAllUnavailable("The selected action is not a Code Fix.");
        }

        var provider = _discoveryService.FindCodeFixProvider(resolution.Action.ProviderId);
        if (provider is null)
        {
            return FixAllUnavailable("The originating code-fix provider is no longer available.");
        }

        var fixAllProvider = provider.GetFixAllProvider();
        if (fixAllProvider is null)
        {
            return FixAllUnavailable("The selected code fix does not expose a fix-all provider.");
        }

        var operation = new FixAllOperation
        {
            Action = resolution.Action,
            OriginDocument = resolution.Document,
            Provider = provider,
            FixAllProvider = fixAllProvider,
        };

        var scopeResolution = _requestResolver.ResolveScope(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var application = await ApplyScopeAsync(
            request.Scope.Kind,
            operation,
            scopeResolution,
            context.CurrentSolution,
            cancellationToken);

        if (application.HasFailure)
        {
            return Rejected<WorkspaceMutationCandidate>(application.Failure);
        }

        var limitRejection = await EnforceChangeLimitAsync(
            request.EffectiveMaxChanges,
            context.CurrentSolution,
            application.CandidateSolution,
            cancellationToken);

        return limitRejection ?? CreateSuccess(operation.Action, application.CandidateSolution);
    }

    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        return _composition.Status.IsAvailable
            ? null
            : Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }

    private ValueTask<CodeActionResolution<WorkspaceMutationCandidate>> ResolveActionAsync(
        StageFixAllRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        return _resolver.ResolveActionAsync<WorkspaceMutationCandidate>(
            request.ActionId,
            request.ExpectedSnapshot,
            context,
            cancellationToken);
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> MapResolutionRejection(
        CodeActionResolutionFailureKind failureKind,
        CodeActionExecutionResult<WorkspaceMutationCandidate> rejection)
    {
        return failureKind == CodeActionResolutionFailureKind.ProviderUnavailable
            ? FixAllUnavailable("The originating code-fix provider is no longer available.")
            : rejection;
    }

    private ValueTask<CodeActionApplyResult> ApplyScopeAsync(
        ScopeKind scopeKind,
        FixAllOperation operation,
        CodeActionScopeResolution scopeResolution,
        Solution currentSolution,
        CancellationToken cancellationToken)
    {
        return scopeKind switch
        {
            ScopeKind.Solution => ApplySolutionAsync(operation, currentSolution, cancellationToken),
            ScopeKind.Document => ApplyDocumentAsync(operation, scopeResolution, currentSolution, cancellationToken),
            ScopeKind.Project => ApplyProjectAsync(operation, scopeResolution, currentSolution, cancellationToken),
            ScopeKind.Projects => ApplyProjectsAsync(operation, scopeResolution, currentSolution, cancellationToken),
            _ => ValueTask.FromResult(FailedApplication(
                CodeActionApplyFailureKind.InvalidRequest,
                "The requested scope kind is not supported for fix-all.")),
        };
    }

    private async ValueTask<CodeActionApplyResult> ApplySolutionAsync(
        FixAllOperation operation,
        Solution currentSolution,
        CancellationToken cancellationToken)
    {
        var originDocument = currentSolution.GetDocument(operation.OriginDocument.Id);
        if (originDocument is null)
        {
            return FailedApplication(
                CodeActionApplyFailureKind.ActionExpired,
                "The requested action reference is no longer valid.");
        }

        return await ApplySolutionFixAllAsync(
            operation,
            originDocument,
            cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplyDocumentAsync(
        FixAllOperation operation,
        CodeActionScopeResolution scopeResolution,
        Solution currentSolution,
        CancellationToken cancellationToken)
    {
        var targetDocument = currentSolution.GetDocument(scopeResolution.Documents[0].Id);
        if (targetDocument is null)
        {
            return FailedApplication(
                CodeActionApplyFailureKind.DocumentNotFound,
                "The document selector did not resolve to a source document.");
        }

        return await ApplyDocumentFixAllAsync(
            operation,
            targetDocument,
            cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplyProjectAsync(
        FixAllOperation operation,
        CodeActionScopeResolution scopeResolution,
        Solution currentSolution,
        CancellationToken cancellationToken)
    {
        var targetProject = currentSolution.GetProject(scopeResolution.Projects[0].Id);
        if (targetProject is null)
        {
            return ProjectNotFound();
        }

        return await ApplyProjectFixAllAsync(
            operation,
            targetProject,
            cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplyProjectsAsync(
        FixAllOperation operation,
        CodeActionScopeResolution scopeResolution,
        Solution workingSolution,
        CancellationToken cancellationToken)
    {
        foreach (var selectedProject in scopeResolution.Projects)
        {
            var targetProject = workingSolution.GetProject(selectedProject.Id);
            if (targetProject is null)
            {
                return ProjectNotFound();
            }

            var application = await ApplyProjectFixAllAsync(
                operation,
                targetProject,
                cancellationToken);

            if (application.HasFailure)
            {
                return application;
            }

            workingSolution = application.CandidateSolution;
        }

        return CodeActionApplyResult.Applied(workingSolution);
    }

    private async ValueTask<CodeActionApplyResult> ApplyDocumentFixAllAsync(
        FixAllOperation operation,
        Document document,
        CancellationToken cancellationToken)
    {
        var creation = await _fixAllActionFactory.CreateDocumentAsync(
            operation.Provider,
            operation.FixAllProvider,
            document,
            operation.Action.DiagnosticIds,
            operation.Action.EquivalenceKey,
            syntheticDiagnosticId: null,
            cancellationToken);

        if (creation.HasFailure)
        {
            return CodeActionApplyResult.Failed(
                CodeActionApplyFailureKind.FixAllUnavailable,
                creation.Failure.Message);
        }

        return await _evaluator.EvaluateAsync(
            creation.Action,
            document.Project.Solution,
            cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplyProjectFixAllAsync(
        FixAllOperation operation,
        Project project,
        CancellationToken cancellationToken)
    {
        var creation = await _fixAllActionFactory.CreateProjectAsync(
            operation.Provider,
            operation.FixAllProvider,
            project,
            operation.Action.DiagnosticIds,
            operation.Action.EquivalenceKey,
            syntheticDiagnosticId: null,
            cancellationToken);

        if (creation.HasFailure)
        {
            return CodeActionApplyResult.Failed(
                CodeActionApplyFailureKind.FixAllUnavailable,
                creation.Failure.Message);
        }

        return await _evaluator.EvaluateAsync(
            creation.Action,
            project.Solution,
            cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplySolutionFixAllAsync(
        FixAllOperation operation,
        Document originDocument,
        CancellationToken cancellationToken)
    {
        var creation = await _fixAllActionFactory.CreateSolutionAsync(
            operation.Provider,
            operation.FixAllProvider,
            originDocument,
            operation.Action.DiagnosticIds,
            operation.Action.EquivalenceKey,
            syntheticDiagnosticId: null,
            cancellationToken);

        if (creation.HasFailure)
        {
            return CodeActionApplyResult.Failed(
                CodeActionApplyFailureKind.FixAllUnavailable,
                creation.Failure.Message);
        }

        return await _evaluator.EvaluateAsync(
            creation.Action,
            originDocument.Project.Solution,
            cancellationToken);
    }

    private async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>?> EnforceChangeLimitAsync(
        int maxChanges,
        Solution originalSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken)
    {
        var changedDocumentCount = await _solutionChangeCounter.CountChangedSourceDocumentsAsync(
            originalSolution,
            candidateSolution,
            cancellationToken);

        if (changedDocumentCount <= maxChanges)
        {
            return null;
        }

        var error = new CodeActionExecutionError
        {
            Code = "FixAllLimitExceeded",
            Message = $"The fix-all operation would change {changedDocumentCount} source documents, exceeding the limit of {maxChanges}.",
        };

        return CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(error, RequiredAction.NarrowRequest);
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> CreateSuccess(
        DiscoveredCodeAction action,
        Solution candidateSolution)
    {
        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = candidateSolution,
            Summary = $"Fix all: {action.Title}",
        };

        return CodeActionExecutionResult.Success(candidate);
    }

    private static CodeActionApplyResult ProjectNotFound()
    {
        return FailedApplication(
            CodeActionApplyFailureKind.ProjectNotFound,
            "The project selector did not resolve to a source project.");
    }

    private static CodeActionApplyResult FailedApplication(
        CodeActionApplyFailureKind kind,
        string message)
    {
        var failure = new CodeActionApplyFailure
        {
            Kind = kind,
            Message = message,
        };

        return CodeActionApplyResult.Failed(failure);
    }

    private sealed record FixAllOperation
    {
        public required DiscoveredCodeAction Action { get; init; }

        public required Document OriginDocument { get; init; }

        public required CodeFixProvider Provider { get; init; }

        public required FixAllProvider FixAllProvider { get; init; }
    }
}
