using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed class ScopedCodeFixStager : IScopedCodeFixStager
{
    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionEvaluator _evaluator;
    private readonly IFixAllActionFactory _fixAllActionFactory;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly IScopedCodeFixCandidateResolver _candidateResolver;
    private readonly ICodeActionToolRequestResolver _requestResolver;
    private readonly ICodeActionSolutionChangeCounter _solutionChangeCounter;

    public ScopedCodeFixStager(
        ICodeActionComposition composition,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionEvaluator evaluator,
        IFixAllActionFactory fixAllActionFactory,
        ICodeActionDiagnosticService diagnosticService,
        IScopedCodeFixCandidateResolver candidateResolver,
        ICodeActionToolRequestResolver requestResolver,
        ICodeActionSolutionChangeCounter solutionChangeCounter)
    {
        _composition = composition;
        _discoveryService = discoveryService;
        _evaluator = evaluator;
        _fixAllActionFactory = fixAllActionFactory;
        _diagnosticService = diagnosticService;
        _candidateResolver = candidateResolver;
        _requestResolver = requestResolver;
        _solutionChangeCounter = solutionChangeCounter;
    }

    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtimeRejection = RejectedIfUnavailable();
        if (runtimeRejection is not null)
        {
            return runtimeRejection;
        }

        var snapshotRejection = _requestResolver.ValidateSnapshot<WorkspaceMutationCandidate>(context, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.DiagnosticIds.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "At least one diagnostic ID is required.");
        }

        var scopeResolution = _requestResolver.ResolveScope(request.Scope, context);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var candidateResolution = await _candidateResolver.ResolveAsync(
            request,
            scopeResolution.Documents,
            context.WorkspaceResolver,
            cancellationToken);

        if (!candidateResolution.IsResolved)
        {
            return MapCandidateResolutionFailure(candidateResolution);
        }

        var candidate = candidateResolution.Candidate;
        var application = await ApplyCandidateAsync(
            candidate,
            request,
            request.Scope.Kind,
            scopeResolution,
            context,
            cancellationToken);

        if (application.HasFailure)
        {
            return Rejected<WorkspaceMutationCandidate>(application.Failure);
        }

        var changedDocumentCount = await _solutionChangeCounter.CountChangedSourceDocumentsAsync(
            context.CurrentSolution,
            application.CandidateSolution,
            cancellationToken);

        if (request.MaxChanges is int maxChanges && changedDocumentCount > maxChanges)
        {
            var error = new CodeActionExecutionError
            {
                Code = "FixAllLimitExceeded",
                Message = $"The fix-all operation would change {changedDocumentCount} source documents, exceeding the limit of {maxChanges}.",
            };

            return CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(
                error,
                RequiredAction.NarrowRequest);
        }

        var mutationCandidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = application.CandidateSolution,
            Summary = candidate.Title,
        };

        return CodeActionExecutionResult.Success(mutationCandidate);
    }

    private ValueTask<CodeActionApplyResult> ApplyCandidateAsync(
        ScopedCodeFixCandidate candidate,
        ScopedCodeFixRequest request,
        ScopeKind scopeKind,
        CodeActionScopeResolution scopeResolution,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (scopeKind == ScopeKind.Solution)
        {
            return ApplySolutionFixAsync(candidate, request, cancellationToken);
        }

        if (scopeKind == ScopeKind.Document)
        {
            return ApplyDocumentFixAsync(candidate, request, scopeResolution, context, cancellationToken);
        }

        if (scopeKind == ScopeKind.Project)
        {
            return ApplyProjectFixAsync(candidate, request, scopeResolution, cancellationToken);
        }

        return ApplyProjectsFixAsync(candidate, request, scopeResolution, context.CurrentSolution, cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplySolutionFixAsync(
        ScopedCodeFixCandidate candidate,
        ScopedCodeFixRequest request,
        CancellationToken cancellationToken)
    {
        var fixAllProvider = candidate.Provider.GetFixAllProvider();
        if (fixAllProvider is null)
        {
            return FailedApplication(
                CodeActionApplyFailureKind.FixAllUnavailable,
                "The selected code fix does not expose a fix-all provider.");
        }

        return await CreateAndEvaluateSolutionFixAllAsync(
            candidate.Provider,
            fixAllProvider,
            candidate.Document,
            candidate.DiagnosticIds,
            candidate.EquivalenceKey,
            request.SyntheticDiagnosticId,
            cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplyDocumentFixAsync(
        ScopedCodeFixCandidate candidate,
        ScopedCodeFixRequest request,
        CodeActionScopeResolution scopeResolution,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var targetDocument = scopeResolution.Documents[0];

        var fixAllProvider = candidate.Provider.GetFixAllProvider();
        if (fixAllProvider is null)
        {
            return await ApplyDocumentScopedCodeFixAsync(
                candidate,
                targetDocument,
                context,
                request.AnalyzerTypeName,
                request.SyntheticDiagnosticId,
                cancellationToken);
        }

        return await CreateAndEvaluateDocumentFixAllAsync(
            candidate.Provider,
            fixAllProvider,
            targetDocument,
            candidate.DiagnosticIds,
            candidate.EquivalenceKey,
            request.SyntheticDiagnosticId,
            cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplyProjectFixAsync(
        ScopedCodeFixCandidate candidate,
        ScopedCodeFixRequest request,
        CodeActionScopeResolution scopeResolution,
        CancellationToken cancellationToken)
    {
        var fixAllProvider = candidate.Provider.GetFixAllProvider();
        if (fixAllProvider is null)
        {
            return FailedApplication(
                CodeActionApplyFailureKind.FixAllUnavailable,
                "The selected code fix does not expose a fix-all provider.");
        }

        var targetProject = scopeResolution.Projects[0];

        return await CreateAndEvaluateProjectFixAllAsync(
            candidate.Provider,
            fixAllProvider,
            targetProject,
            candidate.DiagnosticIds,
            candidate.EquivalenceKey,
            request.SyntheticDiagnosticId,
            cancellationToken);
    }

    private async ValueTask<CodeActionApplyResult> ApplyProjectsFixAsync(
        ScopedCodeFixCandidate candidate,
        ScopedCodeFixRequest request,
        CodeActionScopeResolution scopeResolution,
        Solution workingSolution,
        CancellationToken cancellationToken)
    {
        var fixAllProvider = candidate.Provider.GetFixAllProvider();
        if (fixAllProvider is null)
        {
            return FailedApplication(
                CodeActionApplyFailureKind.FixAllUnavailable,
                "The selected code fix does not expose a fix-all provider.");
        }

        foreach (var selectedProject in scopeResolution.Projects)
        {
            var targetProject = workingSolution.GetProject(selectedProject.Id);
            if (targetProject is null)
            {
                return FailedApplication(
                    CodeActionApplyFailureKind.ProjectNotFound,
                    "The project selector did not resolve to a source project.");
            }

            var fixAllResult = await CreateAndEvaluateProjectFixAllAsync(
                candidate.Provider,
                fixAllProvider,
                targetProject,
                candidate.DiagnosticIds,
                candidate.EquivalenceKey,
                request.SyntheticDiagnosticId,
                cancellationToken);

            if (fixAllResult.HasFailure)
            {
                return fixAllResult;
            }

            workingSolution = fixAllResult.CandidateSolution;
        }

        return CodeActionApplyResult.Applied(workingSolution);
    }

    private async ValueTask<CodeActionApplyResult> ApplyDocumentScopedCodeFixAsync(
        ScopedCodeFixCandidate candidate,
        Document targetDocument,
        ICodeActionExecutionContext context,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var diagnostics = await _diagnosticService.GetScopedCodeFixDiagnosticsAsync(
            targetDocument,
            candidate.DiagnosticIds,
            analyzerTypeName,
            syntheticDiagnosticId,
            cancellationToken);

        if (diagnostics.Count == 0)
        {
            return FailedApplication(
                CodeActionApplyFailureKind.CodeFixUnavailable,
                "No matching code fix was available for the selected scope.");
        }

        var discovered = await _discoveryService.DiscoverCodeFixesAsync(candidate.Provider, targetDocument, diagnostics, cancellationToken);
        var matches = discovered
            .Where(action =>
                string.Equals(action.Title, candidate.Title, StringComparison.OrdinalIgnoreCase)
                && string.Equals(action.EquivalenceKey, candidate.EquivalenceKey, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length == 0)
        {
            return FailedApplication(
                CodeActionApplyFailureKind.CodeFixUnavailable,
                "No matching code fix was available for the selected scope.");
        }

        if (matches.Length > 1)
        {
            return FailedApplication(
                CodeActionApplyFailureKind.ActionAmbiguous,
                "The requested code fix could not be selected uniquely.");
        }

        return await _evaluator.EvaluateAsync(
            matches[0].Action,
            context.CurrentSolution,
            cancellationToken);
    }

    private async Task<CodeActionApplyResult> CreateAndEvaluateDocumentFixAllAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var creation = await _fixAllActionFactory.CreateDocumentAsync(
            provider,
            fixAllProvider,
            document,
            diagnosticIds,
            equivalenceKey,
            syntheticDiagnosticId,
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

    private async Task<CodeActionApplyResult> CreateAndEvaluateProjectFixAllAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Project project,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var creation = await _fixAllActionFactory.CreateProjectAsync(
            provider,
            fixAllProvider,
            project,
            diagnosticIds,
            equivalenceKey,
            syntheticDiagnosticId,
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

    private async Task<CodeActionApplyResult> CreateAndEvaluateSolutionFixAllAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document originDocument,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var creation = await _fixAllActionFactory.CreateSolutionAsync(
            provider,
            fixAllProvider,
            originDocument,
            diagnosticIds,
            equivalenceKey,
            syntheticDiagnosticId,
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

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> MapCandidateResolutionFailure(
        ScopedCodeFixCandidateResolution resolution)
    {
        if (resolution.HasFailure)
        {
            var code = resolution.Outcome == ScopedCodeFixCandidateResolutionOutcome.Ambiguous
                ? "ActionAmbiguous"
                : "CodeFixUnavailable";

            return Rejected<WorkspaceMutationCandidate>(code, resolution.Message);
        }

        return CodeActionExecutionResult.NoChange<WorkspaceMutationCandidate>();
    }

    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        if (_composition.Status.IsAvailable)
        {
            return null;
        }

        return Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }

}
