namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionMutationWorkflow : ICodeActionMutationWorkflow
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionResolutionService _resolutionService;
    private readonly ICodeActionOperationService _operationService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionDescriptorRegistry _descriptorRegistry;
    private readonly ICodeActionTokenService _tokenService;

    public CodeActionMutationWorkflow(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionResolutionService resolutionService,
        ICodeActionOperationService operationService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionDescriptorRegistry descriptorRegistry,
        ICodeActionTokenService tokenService)
    {
        _providerCatalog = providerCatalog;
        _discoveryService = discoveryService;
        _resolutionService = resolutionService;
        _operationService = operationService;
        _diagnosticService = diagnosticService;
        _descriptorRegistry = descriptorRegistry;
        _tokenService = tokenService;
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeActionAsync(
        StageCodeActionRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        var runtimeRejection = RejectedIfUnavailable();
        if (runtimeRejection is not null)
        {
            return ValueTask.FromResult(runtimeRejection);
        }

        return StageAsync(request.ActionId, request.ExpectedSnapshot, DiscoveredActionKind.Refactoring, context, cancellationToken);
    }

    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        ICodeActionMutationContext context,
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

        if (request.Location is null)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "A location selector is required.");
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken).ConfigureAwait(false);
        if (location.Status != SelectorResolveStatus.Resolved || location.Value is null)
        {
            return RejectFromStatus<WorkspaceMutationCandidate>(location.Status, "Location");
        }

        var document = context.CurrentSolution.GetDocument(location.Value.SourceTree);
        if (document is null)
        {
            return Rejected<WorkspaceMutationCandidate>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
        }

        var span = location.Value.SourceSpan;
        var matchingProviders = _discoveryService.GetMatchingRefactoringProviders(request.ProviderId);
        if (matchingProviders.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeActionUnavailable", "No matching refactoring provider is available.");
        }

        var candidates = new List<ClassifiedCodeAction>();
        foreach (var provider in matchingProviders)
        {
            var actions = await _discoveryService.DiscoverRefactoringsAsync(provider, document, span, cancellationToken).ConfigureAwait(false);
            foreach (var action in actions)
            {
                if (!string.IsNullOrWhiteSpace(request.Title)
                    && !string.Equals(action.Title, request.Title, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.TitleStartsWith)
                    && !action.Title.StartsWith(request.TitleStartsWith, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.TitleDoesNotContain)
                    && action.Title.Contains(request.TitleDoesNotContain, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.EquivalenceKey)
                    && !string.Equals(action.EquivalenceKey, request.EquivalenceKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (request.ActionPath is { Count: > 0 }
                    && !action.ActionPath.SequenceEqual(request.ActionPath))
                {
                    continue;
                }

                var descriptor = _descriptorRegistry.Classify(action.Action, action.ProviderId, action.Title);
                if (!descriptor.IsVisible)
                {
                    continue;
                }

                candidates.Add(new ClassifiedCodeAction
                {
                    Action = action,
                    Descriptor = descriptor,
                });
            }
        }

        var distinctCandidates = candidates
            .GroupBy(static candidate => new ReplayCodeActionCandidateKey
            {
                ProviderId = candidate.Action.ProviderId,
                Title = candidate.Action.Title,
                EquivalenceKey = candidate.Action.EquivalenceKey,
                ActionPath = candidate.Action.ActionPath.ToArray(),
            })
            .Select(static group => group.First())
            .ToArray();
        if (distinctCandidates.Length == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeActionUnavailable", "No matching replayable refactoring was available at the selected location.");
        }

        if (distinctCandidates.Length > 1)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionAmbiguous",
                Message = "The requested refactoring could not be selected uniquely.",
            }, RequiredAction.ResolveTargetAgain);
        }

        var candidate = distinctCandidates[0];
        return candidate.Descriptor.ExecutionMode switch
        {
            CodeActionExecutionMode.Replay => await _operationService.CreateMutationCandidateAsync(candidate.Action.Action, candidate.Action.Title, context, cancellationToken).ConfigureAwait(false),
            CodeActionExecutionMode.Parameterised => CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            }),
            _ => Rejected<WorkspaceMutationCandidate>("CodeActionUnavailable", "The selected action is not replayable in this server build.", RequiredAction.ResolveTargetAgain),
        };
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeFixAsync(
        StageCodeFixRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        var runtimeRejection = RejectedIfUnavailable();
        if (runtimeRejection is not null)
        {
            return ValueTask.FromResult(runtimeRejection);
        }

        return StageAsync(request.ActionId, request.ExpectedSnapshot, DiscoveredActionKind.CodeFix, context, cancellationToken);
    }

    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageFixAllAsync(
        StageFixAllRequest request,
        ICodeActionMutationContext context,
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

        if (!_tokenService.TryDecode(request.ActionId, out var payload))
        {
            return ActionExpired<WorkspaceMutationCandidate>();
        }

        if (!string.Equals(payload.Kind, DiscoveredActionKind.CodeFix.ToString(), StringComparison.Ordinal))
        {
            return ActionExpired<WorkspaceMutationCandidate>();
        }

        if (!DateTimeOffset.TryParse(payload.ExpiresAt, out var expiresAt) || expiresAt < DateTimeOffset.UtcNow)
        {
            return ActionExpired<WorkspaceMutationCandidate>();
        }

        if (!string.Equals(payload.WorkspaceId, context.WorkspaceIdentity.WorkspaceId, StringComparison.Ordinal)
            || payload.WorkspaceEpoch != context.WorkspaceIdentity.WorkspaceEpoch
            || payload.TransactionRevision != context.TransactionRevision)
        {
            return ActionExpired<WorkspaceMutationCandidate>();
        }

        var originDocumentResolution = context.WorkspaceResolver.ResolveDocument(new DocumentSelector
        {
            Path = payload.DocumentPath,
        });
        if (originDocumentResolution.Status != SelectorResolveStatus.Resolved || originDocumentResolution.Value is null)
        {
            return ActionExpired<WorkspaceMutationCandidate>();
        }

        var originDocument = originDocumentResolution.Value;
        var originSpan = new TextSpan(payload.Start, payload.Length);
        var provider = _discoveryService.FindCodeFixProvider(payload.ProviderId);
        if (provider is null)
        {
            return FixAllUnavailable("The originating code-fix provider is no longer available.");
        }

        var fixAllProvider = provider.GetFixAllProvider();
        if (fixAllProvider is null)
        {
            return FixAllUnavailable("The selected code fix does not expose a fix-all provider.");
        }

        var diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(originDocument, originSpan, payload.DiagnosticIds, cancellationToken).ConfigureAwait(false);
        var actions = await _discoveryService.DiscoverCodeFixesAsync(provider, originDocument, diagnostics, cancellationToken).ConfigureAwait(false);
        var matches = actions
            .Where(action =>
                string.Equals(action.Title, payload.Title, StringComparison.Ordinal)
                && string.Equals(action.EquivalenceKey, payload.EquivalenceKey, StringComparison.Ordinal)
                && action.ActionPath.SequenceEqual(payload.ActionPath)
                && action.DiagnosticIds.SequenceEqual(payload.DiagnosticIds, StringComparer.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionAmbiguous",
                Message = "The requested action could not be reproduced uniquely.",
            }, RequiredAction.ResolveTargetAgain);
        }

        var workingSolution = context.CurrentSolution;
        switch (request.Scope.Kind)
        {
            case ScopeKind.Solution:
                {
                    var workingOriginDocument = workingSolution.GetDocument(originDocument.Id);
                    if (workingOriginDocument is null)
                    {
                        return ActionExpired<WorkspaceMutationCandidate>();
                    }

                    var fixAllResult = await _operationService.ApplyFixAllAsync(
                        provider,
                        fixAllProvider,
                        workingOriginDocument,
                        originSpan,
                        FixAllScope.Solution,
                        payload.DiagnosticIds,
                        matches[0].EquivalenceKey,
                        syntheticDiagnosticId: null,
                        cancellationToken).ConfigureAwait(false);
                    if (fixAllResult.HasRejection)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution;
                    break;
                }

            case ScopeKind.Document:
                {
                    if (request.Scope.Document is null)
                    {
                        return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "Document scope requires a document selector.");
                    }

                    var documentResolution = context.WorkspaceResolver.ResolveDocument(request.Scope.Document);
                    if (documentResolution.Status != SelectorResolveStatus.Resolved || documentResolution.Value is null)
                    {
                        return RejectFromStatus<WorkspaceMutationCandidate>(documentResolution.Status, "Document");
                    }

                    var targetDocument = workingSolution.GetDocument(documentResolution.Value.Id);
                    if (targetDocument is null)
                    {
                        return Rejected<WorkspaceMutationCandidate>("DocumentNotFound", "The document selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
                    }

                    var fixAllResult = await _operationService.ApplyFixAllAsync(
                        provider,
                        fixAllProvider,
                        targetDocument,
                        originSpan,
                        FixAllScope.Document,
                        payload.DiagnosticIds,
                        matches[0].EquivalenceKey,
                        syntheticDiagnosticId: null,
                        cancellationToken).ConfigureAwait(false);
                    if (fixAllResult.HasRejection)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution;
                    break;
                }

            case ScopeKind.Project:
                {
                    if (request.Scope.Project is null)
                    {
                        return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "Project scope requires a project selector.");
                    }

                    var projectResolution = context.WorkspaceResolver.ResolveProject(request.Scope.Project);
                    if (projectResolution.Status != SelectorResolveStatus.Resolved || projectResolution.Value is null)
                    {
                        return RejectFromStatus<WorkspaceMutationCandidate>(projectResolution.Status, "Project");
                    }

                    var targetProject = workingSolution.GetProject(projectResolution.Value.Id);
                    if (targetProject is null)
                    {
                        return Rejected<WorkspaceMutationCandidate>("ProjectNotFound", "The project selector did not resolve to a source project.", RequiredAction.ResolveTargetAgain);
                    }

                    var fixAllResult = await _operationService.ApplyFixAllAsync(
                        provider,
                        fixAllProvider,
                        targetProject,
                        payload.DiagnosticIds,
                        matches[0].EquivalenceKey,
                        syntheticDiagnosticId: null,
                        cancellationToken).ConfigureAwait(false);
                    if (fixAllResult.HasRejection)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution;
                    break;
                }

            case ScopeKind.Projects:
                {
                    if (request.Scope.Projects is null || request.Scope.Projects.Count == 0)
                    {
                        return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "Projects scope requires at least one project selector.");
                    }

                    foreach (var projectSelector in request.Scope.Projects)
                    {
                        var projectResolution = context.WorkspaceResolver.ResolveProject(projectSelector);
                        if (projectResolution.Status != SelectorResolveStatus.Resolved || projectResolution.Value is null)
                        {
                            return RejectFromStatus<WorkspaceMutationCandidate>(projectResolution.Status, "Project");
                        }

                        var targetProject = workingSolution.GetProject(projectResolution.Value.Id);
                        if (targetProject is null)
                        {
                            return Rejected<WorkspaceMutationCandidate>("ProjectNotFound", "The project selector did not resolve to a source project.", RequiredAction.ResolveTargetAgain);
                        }

                        var fixAllResult = await _operationService.ApplyFixAllAsync(
                            provider,
                            fixAllProvider,
                            targetProject,
                            payload.DiagnosticIds,
                            matches[0].EquivalenceKey,
                            syntheticDiagnosticId: null,
                            cancellationToken).ConfigureAwait(false);
                        if (fixAllResult.HasRejection)
                        {
                            return fixAllResult.Rejection;
                        }

                        workingSolution = fixAllResult.CandidateSolution;
                    }

                    break;
                }

            default:
                return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "The requested scope kind is not supported for fix-all.");
        }

        var changedDocumentCount = await _operationService.CountChangedSourceDocumentsAsync(
            context.CurrentSolution,
            workingSolution,
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
            CandidateSolution = workingSolution,
            Summary = $"Fix all: {matches[0].Title}",
        });
    }

    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        ICodeActionMutationContext context,
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

        var documentsResolution = ResolveScopeDocuments(request.Scope, context);
        if (documentsResolution.Rejection is not null)
        {
            return documentsResolution.Rejection;
        }

        var matchingProviders = _discoveryService.GetMatchingCodeFixProviders(request.ProviderId);
        if (matchingProviders.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code-fix provider is available.");
        }

        var candidates = new List<ScopedCodeFixCandidate>();
        var hadDiagnostics = false;

        foreach (var document in documentsResolution.Documents
            .OrderBy(document => context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name), StringComparer.Ordinal))
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

        if (!hadDiagnostics)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.NoChange();
        }

        var distinctCandidates = candidates
            .GroupBy(candidate => new ScopedCodeFixCandidateKey
            {
                ProviderId = _discoveryService.GetProviderId(candidate.Provider),
                Title = candidate.Title,
                EquivalenceKey = candidate.EquivalenceKey,
                DiagnosticIds = candidate.DiagnosticIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
            })
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
        var workingSolution = context.CurrentSolution;

        switch (request.Scope.Kind)
        {
            case ScopeKind.Solution:
                {
                    var fixAllProvider = candidate.Provider.GetFixAllProvider();
                    if (fixAllProvider is null)
                    {
                        return FixAllUnavailable("The selected code fix does not expose a fix-all provider.");
                    }

                    var originDocument = workingSolution.GetDocument(candidate.Document.Id);
                    if (originDocument is null)
                    {
                        return Rejected<WorkspaceMutationCandidate>("DocumentNotFound", "The selected scope could not be resolved to a source document.", RequiredAction.ResolveTargetAgain);
                    }

                    var fixAllResult = await _operationService.ApplyFixAllAsync(
                        candidate.Provider,
                        fixAllProvider,
                        originDocument,
                        candidate.DocumentSpan,
                        FixAllScope.Solution,
                        candidate.DiagnosticIds,
                        candidate.EquivalenceKey,
                        request.SyntheticDiagnosticId,
                        cancellationToken).ConfigureAwait(false);
                    if (fixAllResult.HasRejection)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution;
                    break;
                }

            case ScopeKind.Document:
                {
                    if (request.Scope.Document is null)
                    {
                        return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "Document scope requires a document selector.");
                    }

                    var documentResolution = context.WorkspaceResolver.ResolveDocument(request.Scope.Document);
                    if (documentResolution.Status != SelectorResolveStatus.Resolved || documentResolution.Value is null)
                    {
                        return RejectFromStatus<WorkspaceMutationCandidate>(documentResolution.Status, "Document");
                    }

                    var targetDocument = workingSolution.GetDocument(documentResolution.Value.Id);
                    if (targetDocument is null)
                    {
                        return Rejected<WorkspaceMutationCandidate>("DocumentNotFound", "The document selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
                    }

                    var fixAllProvider = candidate.Provider.GetFixAllProvider();
                    if (fixAllProvider is null)
                    {
                        var directResult = await ApplyDocumentScopedCodeFixAsync(
                            candidate,
                            targetDocument,
                            context,
                            request.AnalyzerTypeName,
                            request.SyntheticDiagnosticId,
                            cancellationToken).ConfigureAwait(false);
                        if (directResult.HasRejection)
                        {
                            return directResult.Rejection;
                        }

                        workingSolution = directResult.CandidateSolution;
                        break;
                    }

                    var fixAllResult = await _operationService.ApplyFixAllAsync(
                        candidate.Provider,
                        fixAllProvider,
                        targetDocument,
                        candidate.DocumentSpan,
                        FixAllScope.Document,
                        candidate.DiagnosticIds,
                        candidate.EquivalenceKey,
                        request.SyntheticDiagnosticId,
                        cancellationToken).ConfigureAwait(false);
                    if (fixAllResult.HasRejection)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution;
                    break;
                }

            case ScopeKind.Project:
                {
                    var fixAllProvider = candidate.Provider.GetFixAllProvider();
                    if (fixAllProvider is null)
                    {
                        return FixAllUnavailable("The selected code fix does not expose a fix-all provider.");
                    }

                    if (request.Scope.Project is null)
                    {
                        return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "Project scope requires a project selector.");
                    }

                    var projectResolution = context.WorkspaceResolver.ResolveProject(request.Scope.Project);
                    if (projectResolution.Status != SelectorResolveStatus.Resolved || projectResolution.Value is null)
                    {
                        return RejectFromStatus<WorkspaceMutationCandidate>(projectResolution.Status, "Project");
                    }

                    var targetProject = workingSolution.GetProject(projectResolution.Value.Id);
                    if (targetProject is null)
                    {
                        return Rejected<WorkspaceMutationCandidate>("ProjectNotFound", "The project selector did not resolve to a source project.", RequiredAction.ResolveTargetAgain);
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
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution;
                    break;
                }

            case ScopeKind.Projects:
                {
                    var fixAllProvider = candidate.Provider.GetFixAllProvider();
                    if (fixAllProvider is null)
                    {
                        return FixAllUnavailable("The selected code fix does not expose a fix-all provider.");
                    }

                    if (request.Scope.Projects is null || request.Scope.Projects.Count == 0)
                    {
                        return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "Projects scope requires at least one project selector.");
                    }

                    foreach (var projectSelector in request.Scope.Projects)
                    {
                        var projectResolution = context.WorkspaceResolver.ResolveProject(projectSelector);
                        if (projectResolution.Status != SelectorResolveStatus.Resolved || projectResolution.Value is null)
                        {
                            return RejectFromStatus<WorkspaceMutationCandidate>(projectResolution.Status, "Project");
                        }

                        var targetProject = workingSolution.GetProject(projectResolution.Value.Id);
                        if (targetProject is null)
                        {
                            return Rejected<WorkspaceMutationCandidate>("ProjectNotFound", "The project selector did not resolve to a source project.", RequiredAction.ResolveTargetAgain);
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
                            return fixAllResult.Rejection;
                        }

                        workingSolution = fixAllResult.CandidateSolution;
                    }

                    break;
                }

            default:
                return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "The requested scope kind is not supported for scoped code-fix staging.");
        }

        var changedDocumentCount = await _operationService.CountChangedSourceDocumentsAsync(
            context.CurrentSolution,
            workingSolution,
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
            CandidateSolution = workingSolution,
            Summary = candidate.Title,
        });
    }

    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        ICodeActionMutationContext context,
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

        if (request.Location is null)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "A location selector is required.");
        }

        if (request.DiagnosticIds.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "At least one diagnostic ID is required.");
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken).ConfigureAwait(false);
        if (location.Status != SelectorResolveStatus.Resolved || location.Value is null)
        {
            return RejectFromStatus<WorkspaceMutationCandidate>(location.Status, "Location");
        }

        var document = context.CurrentSolution.GetDocument(location.Value.SourceTree);
        if (document is null)
        {
            return Rejected<WorkspaceMutationCandidate>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
        }

        var span = location.Value.SourceSpan;
        var matchingProviders = _discoveryService.GetMatchingCodeFixProviders(request.ProviderId);
        if (matchingProviders.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code-fix provider is available.");
        }

        var diagnostics = await _diagnosticService.GetLocationScopedCodeFixDiagnosticsAsync(
            document,
            span,
            request.DiagnosticIds,
            request.AnalyzerTypeName,
            request.SyntheticDiagnosticId,
            cancellationToken).ConfigureAwait(false);
        if (diagnostics.IsDefaultOrEmpty)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available at the selected location.");
        }

        var candidates = new List<ClassifiedCodeAction>();
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

                var descriptor = _descriptorRegistry.Classify(action.Action, action.ProviderId, action.Title);
                if (!descriptor.IsVisible)
                {
                    continue;
                }

                candidates.Add(new ClassifiedCodeAction
                {
                    Action = action,
                    Descriptor = descriptor,
                });
            }
        }

        var distinctCandidates = candidates
            .GroupBy(static candidate => new DirectCodeFixCandidateKey
            {
                ProviderId = candidate.Action.ProviderId,
                Title = candidate.Action.Title,
                EquivalenceKey = candidate.Action.EquivalenceKey,
                ActionPath = candidate.Action.ActionPath.ToArray(),
                DiagnosticIds = candidate.Action.DiagnosticIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
            })
            .Select(static group => group.First())
            .ToArray();
        if (distinctCandidates.Length == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available at the selected location.");
        }

        if (distinctCandidates.Length > 1)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionAmbiguous",
                Message = "The requested code fix could not be selected uniquely.",
            }, RequiredAction.ResolveTargetAgain);
        }

        var candidate = distinctCandidates[0];
        return candidate.Descriptor.ExecutionMode switch
        {
            CodeActionExecutionMode.Replay => await _operationService.CreateMutationCandidateAsync(candidate.Action.Action, candidate.Action.Title, context, cancellationToken).ConfigureAwait(false),
            CodeActionExecutionMode.Parameterised => await _operationService.CreateMutationCandidateAsync(candidate.Action.Action, candidate.Action.Title, context, cancellationToken).ConfigureAwait(false),
            _ => Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "The selected action is not replayable in this server build.", RequiredAction.ResolveTargetAgain),
        };
    }

    private async ValueTask<CodeActionApplyResult> ApplyDocumentScopedCodeFixAsync(
        ScopedCodeFixCandidate candidate,
        Document targetDocument,
        ICodeActionMutationContext context,
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
            return new CodeActionApplyResult
            {
                Rejection = Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available for the selected scope."),
            };
        }

        var sourceText = await targetDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var documentSpan = new TextSpan(0, sourceText.Length);
        var discovered = await _discoveryService.DiscoverCodeFixesAsync(candidate.Provider, targetDocument, diagnostics, cancellationToken).ConfigureAwait(false);
        var matches = discovered
            .Where(action =>
                string.Equals(action.Title, candidate.Title, StringComparison.OrdinalIgnoreCase)
                && string.Equals(action.EquivalenceKey, candidate.EquivalenceKey, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            return new CodeActionApplyResult
            {
                Rejection = Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available for the selected scope."),
            };
        }

        if (matches.Length > 1)
        {
            return new CodeActionApplyResult
            {
                Rejection = CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
                {
                    Code = "ActionAmbiguous",
                    Message = "The requested code fix could not be selected uniquely.",
                }),
            };
        }

        var proposalResult = await _operationService.CreateMutationCandidateAsync(matches[0].Action, matches[0].Title, context, cancellationToken).ConfigureAwait(false);
        if (proposalResult.Outcome != CodeActionExecutionOutcome.Succeeded || proposalResult.Data?.CandidateSolution is null)
        {
            return new CodeActionApplyResult
            {
                Rejection = proposalResult,
            };
        }

        return new CodeActionApplyResult
        {
            CandidateSolution = proposalResult.Data.CandidateSolution,
        };
    }

    private async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageAsync(
        string actionId,
        SnapshotPrecondition? expectedSnapshot,
        DiscoveredActionKind expectedKind,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        var resolvedAction = await _resolutionService.ResolveActionAsync<WorkspaceMutationCandidate>(
            actionId,
            expectedSnapshot,
            expectedKind,
            context,
            cancellationToken).ConfigureAwait(false);
        if (resolvedAction.HasRejection)
        {
            return resolvedAction.Rejection;
        }

        if (resolvedAction.Descriptor.ExecutionMode == CodeActionExecutionMode.Parameterised)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            });
        }

        return await _operationService.CreateMutationCandidateAsync(
            resolvedAction.Action.Action,
            resolvedAction.Action.Title,
            context,
            cancellationToken).ConfigureAwait(false);
    }

    private static ScopeDocumentResolution ResolveScopeDocuments(ScopeSelector scope, ICodeActionMutationContext context)
    {
        return scope.Kind switch
        {
            ScopeKind.Solution => new ScopeDocumentResolution
            {
                Documents = context.CurrentSolution.Projects.SelectMany(static project => project.Documents).ToArray(),
            },
            ScopeKind.Document => ResolveDocumentScope(scope.Document, context),
            ScopeKind.Project => ResolveProjectScope(scope.Project, context),
            ScopeKind.Projects => ResolveProjectsScope(scope.Projects, context),
            _ => new ScopeDocumentResolution
            {
                Rejection = Rejected<WorkspaceMutationCandidate>("InvalidRequest", "The requested scope kind is not supported."),
            },
        };
    }

    private static ScopeDocumentResolution ResolveDocumentScope(DocumentSelector? selector, ICodeActionMutationContext context)
    {
        if (selector is null)
        {
            return new ScopeDocumentResolution
            {
                Rejection = Rejected<WorkspaceMutationCandidate>("InvalidRequest", "Document scope requires a document selector."),
            };
        }

        var resolution = context.WorkspaceResolver.ResolveDocument(selector);
        return resolution.Status == SelectorResolveStatus.Resolved && resolution.Value is not null
            ? new ScopeDocumentResolution
            {
                Documents = [resolution.Value],
            }
            : new ScopeDocumentResolution
            {
                Rejection = RejectFromStatus<WorkspaceMutationCandidate>(resolution.Status, "Document"),
            };
    }

    private static ScopeDocumentResolution ResolveProjectScope(ProjectSelector? selector, ICodeActionMutationContext context)
    {
        if (selector is null)
        {
            return new ScopeDocumentResolution
            {
                Rejection = Rejected<WorkspaceMutationCandidate>("InvalidRequest", "Project scope requires a project selector."),
            };
        }

        var resolution = context.WorkspaceResolver.ResolveProject(selector);
        return resolution.Status == SelectorResolveStatus.Resolved && resolution.Value is not null
            ? new ScopeDocumentResolution
            {
                Documents = resolution.Value.Documents.ToArray(),
            }
            : new ScopeDocumentResolution
            {
                Rejection = RejectFromStatus<WorkspaceMutationCandidate>(resolution.Status, "Project"),
            };
    }

    private static ScopeDocumentResolution ResolveProjectsScope(IReadOnlyList<ProjectSelector>? selectors, ICodeActionMutationContext context)
    {
        if (selectors is null || selectors.Count == 0)
        {
            return new ScopeDocumentResolution
            {
                Rejection = Rejected<WorkspaceMutationCandidate>("InvalidRequest", "Projects scope requires at least one project selector."),
            };
        }

        var documents = new List<Document>();
        foreach (var selector in selectors)
        {
            var resolution = context.WorkspaceResolver.ResolveProject(selector);
            if (resolution.Status != SelectorResolveStatus.Resolved || resolution.Value is null)
            {
                return new ScopeDocumentResolution
                {
                    Rejection = RejectFromStatus<WorkspaceMutationCandidate>(resolution.Status, "Project"),
                };
            }

            documents.AddRange(resolution.Value.Documents);
        }

        return new ScopeDocumentResolution
        {
            Documents = documents.DistinctBy(static document => document.Id).ToArray(),
        };
    }

    private static CodeActionExecutionResult<T>? ValidateSnapshot<T>(IWorkspaceResolver resolver, SnapshotPrecondition? expectedSnapshot)
    {
        var result = resolver.ValidateSnapshot(expectedSnapshot);
        return result.Kind == SnapshotMatchKind.Matched
            ? null
            : CodeActionExecutionResult<T>.Conflict(new CodeActionExecutionError
            {
                Code = "SnapshotMismatch",
                Message = "The request snapshot does not match the current workspace snapshot.",
            }, RequiredAction.ResolveTargetAgain);
    }

    private static CodeActionExecutionResult<T> RejectFromStatus<T>(SelectorResolveStatus status, string targetName)
    {
        return status switch
        {
            SelectorResolveStatus.Ambiguous => Rejected<T>($"{targetName}Ambiguous", $"The {targetName.ToLowerInvariant()} selector matched multiple results.", RequiredAction.ResolveTargetAgain),
            _ => Rejected<T>($"{targetName}NotFound", $"The {targetName.ToLowerInvariant()} selector did not match any result.", RequiredAction.ResolveTargetAgain),
        };
    }

    private static CodeActionExecutionResult<T> Rejected<T>(string code, string message, RequiredAction? requiredAction = null)
    {
        return CodeActionExecutionResult<T>.Rejected(new CodeActionExecutionError
        {
            Code = code,
            Message = message,
        }, requiredAction);
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> FixAllUnavailable(string message)
    {
        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
        {
            Code = "FixAllUnavailable",
            Message = message,
        });
    }

    private static CodeActionExecutionResult<T> ActionExpired<T>()
    {
        return CodeActionExecutionResult<T>.Rejected(new CodeActionExecutionError
        {
            Code = "ActionExpired",
            Message = "The requested action token is no longer valid.",
        }, RequiredAction.ResolveTargetAgain);
    }

    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        return _providerCatalog.Status.IsAvailable
            ? null
            : Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }

    private sealed record ScopeDocumentResolution
    {
        public CodeActionExecutionResult<WorkspaceMutationCandidate>? Rejection { get; init; }

        public IReadOnlyList<Document> Documents { get; init; } = [];
    }

    private sealed record ClassifiedCodeAction
    {
        public required DiscoveredCodeAction Action { get; init; }

        public required CodeActionDescriptorEntry Descriptor { get; init; }
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

    private sealed record ScopedCodeFixCandidateKey
    {
        public required string ProviderId { get; init; }

        public required string Title { get; init; }

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<string> DiagnosticIds { get; init; } = [];
    }

    private sealed record ReplayCodeActionCandidateKey
    {
        public required string ProviderId { get; init; }

        public required string Title { get; init; }

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<int> ActionPath { get; init; } = [];
    }

    private sealed record DirectCodeFixCandidateKey
    {
        public required string ProviderId { get; init; }

        public required string Title { get; init; }

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<int> ActionPath { get; init; } = [];

        public IReadOnlyList<string> DiagnosticIds { get; init; } = [];
    }
}
