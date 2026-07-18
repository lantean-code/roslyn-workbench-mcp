using static Roslyn.Workbench.Mcp.CodeActions.Execution.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionFixAllService : ICodeActionFixAllService
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionResolutionService _resolutionService;
    private readonly ICodeActionOperationService _operationService;
    private readonly ICodeActionScopeResolver _scopeResolver;
    private readonly ICodeActionSolutionChangeCounter _solutionChangeCounter;

    public CodeActionFixAllService(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionResolutionService resolutionService,
        ICodeActionOperationService operationService,
        ICodeActionScopeResolver scopeResolver,
        ICodeActionSolutionChangeCounter solutionChangeCounter)
    {
        _providerCatalog = providerCatalog;
        _discoveryService = discoveryService;
        _resolutionService = resolutionService;
        _operationService = operationService;
        _scopeResolver = scopeResolver;
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

        var scope = request.Scope;
        if (scope is null)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "A scope selector is required.");
        }

        var resolution = await ResolveActionAsync(request, context, cancellationToken);
        if (resolution.HasRejection)
        {
            return MapResolutionRejection(resolution.FailureKind, resolution.Rejection);
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
            OriginSpan = resolution.Span,
            Provider = provider,
            FixAllProvider = fixAllProvider,
        };

        var scopeResolution = _scopeResolver.Resolve(
            scope,
            context.CurrentSolution,
            context.WorkspaceResolver);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var application = await ApplyScopeAsync(
            scope.Kind,
            operation,
            scopeResolution,
            context.CurrentSolution,
            cancellationToken);
        if (application.HasRejection)
        {
            return application.Rejection;
        }

        var limitRejection = await EnforceChangeLimitAsync(
            request.MaxChanges,
            context.CurrentSolution,
            application.CandidateSolution,
            cancellationToken);
        return limitRejection ?? CreateSuccess(operation.Action, application.CandidateSolution);
    }

    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        return _providerCatalog.Status.IsAvailable
            ? null
            : Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }

    private ValueTask<CodeActionResolution<WorkspaceMutationCandidate>> ResolveActionAsync(
        StageFixAllRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        return _resolutionService.ResolveActionAsync<WorkspaceMutationCandidate>(
            request.ActionId,
            request.ExpectedSnapshot,
            DiscoveredActionKind.CodeFix,
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
            _ => ValueTask.FromResult(RejectedApplication(Rejected<WorkspaceMutationCandidate>(
                "InvalidRequest",
                "The requested scope kind is not supported for fix-all."))),
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
            return RejectedApplication(ActionExpired<WorkspaceMutationCandidate>());
        }

        return await ApplyDocumentFixAllAsync(
            operation,
            originDocument,
            FixAllScope.Solution,
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
            return RejectedApplication(Rejected<WorkspaceMutationCandidate>(
                "DocumentNotFound",
                "The document selector did not resolve to a source document.",
                RequiredAction.ResolveTargetAgain));
        }

        return await ApplyDocumentFixAllAsync(
            operation,
            targetDocument,
            FixAllScope.Document,
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
            return RejectedApplication(ProjectNotFound());
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
                return RejectedApplication(ProjectNotFound());
            }

            var application = await ApplyProjectFixAllAsync(
                operation,
                targetProject,
                cancellationToken);
            if (application.HasRejection)
            {
                return application;
            }

            workingSolution = application.CandidateSolution;
        }

        return new CodeActionApplyResult
        {
            CandidateSolution = workingSolution,
        };
    }

    private async ValueTask<CodeActionApplyResult> ApplyDocumentFixAllAsync(
        FixAllOperation operation,
        Document document,
        FixAllScope scope,
        CancellationToken cancellationToken)
    {
        return await _operationService.ApplyFixAllAsync(
            operation.Provider,
            operation.FixAllProvider,
            document,
            operation.OriginSpan,
            scope,
            operation.Action.DiagnosticIds,
            operation.Action.EquivalenceKey,
            syntheticDiagnosticId: null,
            cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplyProjectFixAllAsync(
        FixAllOperation operation,
        Project project,
        CancellationToken cancellationToken)
    {
        return await _operationService.ApplyFixAllAsync(
            operation.Provider,
            operation.FixAllProvider,
            project,
            operation.Action.DiagnosticIds,
            operation.Action.EquivalenceKey,
            syntheticDiagnosticId: null,
            cancellationToken);
    }

    private async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>?> EnforceChangeLimitAsync(
        int? maxChanges,
        Solution originalSolution,
        Solution candidateSolution,
        CancellationToken cancellationToken)
    {
        var changedDocumentCount = await _solutionChangeCounter.CountChangedSourceDocumentsAsync(
            originalSolution,
            candidateSolution,
            cancellationToken);
        if (maxChanges is null || changedDocumentCount <= maxChanges.Value)
        {
            return null;
        }

        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
        {
            Code = "FixAllLimitExceeded",
            Message = $"The fix-all operation would change {changedDocumentCount} source documents, exceeding the limit of {maxChanges.Value}.",
        }, RequiredAction.NarrowRequest);
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> CreateSuccess(
        DiscoveredCodeAction action,
        Solution candidateSolution)
    {
        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(new WorkspaceMutationCandidate
        {
            CandidateSolution = candidateSolution,
            Summary = $"Fix all: {action.Title}",
        });
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> ProjectNotFound()
    {
        return Rejected<WorkspaceMutationCandidate>(
            "ProjectNotFound",
            "The project selector did not resolve to a source project.",
            RequiredAction.ResolveTargetAgain);
    }

    private static CodeActionApplyResult RejectedApplication(
        CodeActionExecutionResult<WorkspaceMutationCandidate> rejection)
    {
        return new CodeActionApplyResult
        {
            Rejection = rejection,
        };
    }

    private sealed record FixAllOperation
    {
        public required DiscoveredCodeAction Action { get; init; }

        public required Document OriginDocument { get; init; }

        public required TextSpan OriginSpan { get; init; }

        public required CodeFixProvider Provider { get; init; }

        public required FixAllProvider FixAllProvider { get; init; }
    }
}
