using static Roslyn.Workbench.Mcp.CodeActions.Execution.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionScopedFixService : ICodeActionScopedFixService
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionOperationService _operationService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionScopeResolver _scopeResolver;
    private readonly ICodeActionSolutionChangeCounter _solutionChangeCounter;

    public CodeActionScopedFixService(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionOperationService operationService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionScopeResolver scopeResolver,
        ICodeActionSolutionChangeCounter solutionChangeCounter)
    {
        _providerCatalog = providerCatalog;
        _discoveryService = discoveryService;
        _operationService = operationService;
        _diagnosticService = diagnosticService;
        _scopeResolver = scopeResolver;
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

        var snapshotRejection = ValidateSnapshot<WorkspaceMutationCandidate>(context.WorkspaceResolver, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Scope is null)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "A scope selector is required.");
        }

        if (request.DiagnosticIds.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "At least one diagnostic ID is required.");
        }

        var scopeResolution = _scopeResolver.Resolve(
            request.Scope,
            context.CurrentSolution,
            context.WorkspaceResolver);
        if (scopeResolution.HasRejection)
        {
            return scopeResolution.Rejection;
        }

        var matchingProviders = _discoveryService.GetMatchingCodeFixProviders(request.ProviderId);
        if (matchingProviders.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code-fix provider is available.");
        }

        var discovery = await DiscoverCandidatesAsync(
            request,
            scopeResolution.Documents,
            matchingProviders,
            context.WorkspaceResolver,
            cancellationToken).ConfigureAwait(false);
        if (!discovery.HadDiagnostics)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.NoChange();
        }

        var distinctCandidates = discovery.Candidates
            .GroupBy(candidate => new CodeActionCandidateIdentity(
                _discoveryService.GetProviderId(candidate.Provider),
                candidate.Title,
                candidate.EquivalenceKey,
                diagnosticIds: candidate.DiagnosticIds))
            .Select(static group => group.First())
            .ToArray();
        if (distinctCandidates.Length == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available for the selected scope.");
        }

        if (distinctCandidates.Length > 1)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionAmbiguous",
                Message = "The requested code fix could not be selected uniquely.",
            });
        }

        var candidate = distinctCandidates[0];
        var application = await ApplyCandidateAsync(
            candidate,
            request,
            request.Scope.Kind,
            scopeResolution,
            context,
            cancellationToken).ConfigureAwait(false);
        if (application.HasRejection)
        {
            return application.Rejection;
        }

        var changedDocumentCount = await _solutionChangeCounter.CountChangedSourceDocumentsAsync(
            context.CurrentSolution,
            application.CandidateSolution,
            cancellationToken).ConfigureAwait(false);
        if (request.MaxChanges is int maxChanges && changedDocumentCount > maxChanges)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "FixAllLimitExceeded",
                Message = $"The fix-all operation would change {changedDocumentCount} source documents, exceeding the limit of {maxChanges}.",
            }, RequiredAction.NarrowRequest);
        }

        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(new WorkspaceMutationCandidate
        {
            CandidateSolution = application.CandidateSolution,
            Summary = candidate.Title,
        });
    }

    private async ValueTask<ScopedCandidateDiscovery> DiscoverCandidatesAsync(
        ScopedCodeFixRequest request,
        IReadOnlyList<Document> documents,
        IReadOnlyList<CodeFixProvider> matchingProviders,
        IWorkspaceResolver workspaceResolver,
        CancellationToken cancellationToken)
    {
        var candidates = new List<ScopedCodeFixCandidate>();
        var hadDiagnostics = false;
        var orderedDocuments = documents.OrderBy(
            document => workspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name),
            StringComparer.Ordinal);

        foreach (var document in orderedDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var diagnostics = await _diagnosticService.GetScopedCodeFixDiagnosticsAsync(
                document,
                request.DiagnosticIds,
                request.AnalyzerTypeName,
                request.SyntheticDiagnosticId,
                cancellationToken).ConfigureAwait(false);
            if (diagnostics.IsDefaultOrEmpty)
            {
                continue;
            }

            hadDiagnostics = true;
            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var documentSpan = new TextSpan(0, sourceText.Length);

            foreach (var provider in matchingProviders)
            {
                var actions = await _discoveryService.DiscoverCodeFixesAsync(provider, document, diagnostics, cancellationToken).ConfigureAwait(false);
                foreach (var action in actions)
                {
                    if (!string.IsNullOrWhiteSpace(request.Title)
                        && !string.Equals(action.Title, request.Title, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(request.EquivalenceKey)
                        && !string.Equals(action.EquivalenceKey, request.EquivalenceKey, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    candidates.Add(new ScopedCodeFixCandidate
                    {
                        Document = document,
                        DocumentSpan = documentSpan,
                        Provider = provider,
                        Title = action.Title,
                        EquivalenceKey = action.EquivalenceKey,
                        DiagnosticIds = action.DiagnosticIds,
                    });
                }
            }
        }

        return new ScopedCandidateDiscovery
        {
            Candidates = candidates,
            HadDiagnostics = hadDiagnostics,
        };
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
            return RejectedApplication(FixAllUnavailable("The selected code fix does not expose a fix-all provider."));
        }

        return await _operationService.ApplyFixAllAsync(
            candidate.Provider,
            fixAllProvider,
            candidate.Document,
            candidate.DocumentSpan,
            FixAllScope.Solution,
            candidate.DiagnosticIds,
            candidate.EquivalenceKey,
            request.SyntheticDiagnosticId,
            cancellationToken).ConfigureAwait(false);
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
                cancellationToken).ConfigureAwait(false);
        }

        return await _operationService.ApplyFixAllAsync(
            candidate.Provider,
            fixAllProvider,
            targetDocument,
            candidate.DocumentSpan,
            FixAllScope.Document,
            candidate.DiagnosticIds,
            candidate.EquivalenceKey,
            request.SyntheticDiagnosticId,
            cancellationToken).ConfigureAwait(false);
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
            return RejectedApplication(FixAllUnavailable("The selected code fix does not expose a fix-all provider."));
        }

        var targetProject = scopeResolution.Projects[0];

        return await _operationService.ApplyFixAllAsync(
            candidate.Provider,
            fixAllProvider,
            targetProject,
            candidate.DiagnosticIds,
            candidate.EquivalenceKey,
            request.SyntheticDiagnosticId,
            cancellationToken).ConfigureAwait(false);
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
            return RejectedApplication(FixAllUnavailable("The selected code fix does not expose a fix-all provider."));
        }

        foreach (var selectedProject in scopeResolution.Projects)
        {
            var targetProject = workingSolution.GetProject(selectedProject.Id);
            if (targetProject is null)
            {
                return RejectedApplication(Rejected<WorkspaceMutationCandidate>(
                    "ProjectNotFound",
                    "The project selector did not resolve to a source project.",
                    RequiredAction.ResolveTargetAgain));
            }

            var fixAllResult = await _operationService.ApplyFixAllAsync(
                candidate.Provider,
                fixAllProvider,
                targetProject,
                candidate.DiagnosticIds,
                candidate.EquivalenceKey,
                request.SyntheticDiagnosticId,
                cancellationToken).ConfigureAwait(false);
            if (fixAllResult.HasRejection)
            {
                return fixAllResult;
            }

            workingSolution = fixAllResult.CandidateSolution;
        }

        return new CodeActionApplyResult
        {
            CandidateSolution = workingSolution,
        };
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
            cancellationToken).ConfigureAwait(false);
        if (diagnostics.IsDefaultOrEmpty)
        {
            return RejectedApplication(Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available for the selected scope."));
        }

        var discovered = await _discoveryService.DiscoverCodeFixesAsync(candidate.Provider, targetDocument, diagnostics, cancellationToken).ConfigureAwait(false);
        var matches = discovered
            .Where(action =>
                string.Equals(action.Title, candidate.Title, StringComparison.OrdinalIgnoreCase)
                && string.Equals(action.EquivalenceKey, candidate.EquivalenceKey, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            return RejectedApplication(Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available for the selected scope."));
        }

        if (matches.Length > 1)
        {
            return RejectedApplication(CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionAmbiguous",
                Message = "The requested code fix could not be selected uniquely.",
            }));
        }

        var proposalResult = await _operationService.CreateMutationCandidateAsync(matches[0].Action, matches[0].Title, context, cancellationToken).ConfigureAwait(false);
        if (proposalResult.Outcome != CodeActionExecutionOutcome.Succeeded || proposalResult.Data?.CandidateSolution is null)
        {
            return RejectedApplication(proposalResult);
        }

        return new CodeActionApplyResult
        {
            CandidateSolution = proposalResult.Data.CandidateSolution,
        };
    }

    private static CodeActionApplyResult RejectedApplication(CodeActionExecutionResult<WorkspaceMutationCandidate> rejection)
    {
        return new CodeActionApplyResult
        {
            Rejection = rejection,
        };
    }

    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        return _providerCatalog.Status.IsAvailable
            ? null
            : Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }

    private sealed record ScopedCandidateDiscovery
    {
        public IReadOnlyList<ScopedCodeFixCandidate> Candidates { get; init; } = [];

        public bool HadDiagnostics { get; init; }
    }

    private sealed record ScopedCodeFixCandidate
    {
        public required Document Document { get; init; }

        public required TextSpan DocumentSpan { get; init; }

        public required CodeFixProvider Provider { get; init; }

        public required string Title { get; init; }

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<string> DiagnosticIds { get; init; } = [];
    }
}
