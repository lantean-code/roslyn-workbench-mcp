using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class MefCodeActionService : ICodeActionService
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyList<CodeRefactoringProvider> _refactoringProviders;
    private readonly IReadOnlyList<CodeFixProvider> _codeFixProviders;
    private readonly CodeActionDescriptorRegistry _descriptorRegistry;
    private readonly TimeSpan _tokenLifetime;
    private readonly byte[] _secret;

    public MefCodeActionService(
        HostServices hostServices,
        IReadOnlyList<CodeRefactoringProvider> refactoringProviders,
        IReadOnlyList<CodeFixProvider> codeFixProviders,
        TimeSpan tokenLifetime)
    {
        _refactoringProviders = refactoringProviders;
        _codeFixProviders = codeFixProviders;
        _descriptorRegistry = new CodeActionDescriptorRegistry();
        _tokenLifetime = tokenLifetime;
        _secret = RandomNumberGenerator.GetBytes(32);
        Status = new ComponentStatus
        {
            IsAvailable = true,
            Version = typeof(Microsoft.CodeAnalysis.Workspace).Assembly.GetName().Version?.ToString(),
            Message = $"Composed {_refactoringProviders.Count} refactoring providers and {_codeFixProviders.Count} code-fix providers.",
        };
    }

    public ComponentStatus Status { get; }

    public async ValueTask<PluginExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshotRejection = ValidateSnapshot<CodeActionListData>(context.WorkspaceResolver, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Location is null)
        {
            return Rejected<CodeActionListData>("InvalidRequest", "A location selector is required.");
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken);
        if (location.Status != SelectorResolveStatus.Resolved || location.Value is null)
        {
            return RejectFromStatus<CodeActionListData>(location.Status, "Location");
        }

        var document = context.CurrentSolution.GetDocument(location.Value.SourceTree);
        if (document is null)
        {
            return Rejected<CodeActionListData>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
        }

        var span = location.Value.SourceSpan;
        var discovered = new List<DiscoveredCodeAction>();

        if (request.IncludeRefactorings)
        {
            foreach (var provider in _refactoringProviders.OrderBy(GetProviderId, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                discovered.AddRange(await DiscoverRefactoringsAsync(provider, document, span, cancellationToken));
            }
        }

        if (request.IncludeCodeFixes)
        {
            var diagnostics = await GetDocumentDiagnosticsAsync(document, span, request.DiagnosticIds, cancellationToken);
            foreach (var provider in _codeFixProviders.OrderBy(GetProviderId, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                discovered.AddRange(await DiscoverCodeFixesAsync(provider, document, span, diagnostics, cancellationToken));
            }
        }

        var ordered = discovered
            .Select(action => new ClassifiedCodeAction
            {
                Action = action,
                Descriptor = _descriptorRegistry.Classify(action.Action, action.ProviderId, action.Title),
            })
            .Where(static action => action.Descriptor.IsVisible)
            .OrderBy(static action => action.Action.Title, StringComparer.Ordinal)
            .ThenBy(static action => action.Action.ProviderId, StringComparer.Ordinal)
            .ThenBy(static action => action.Action.EquivalenceKey ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static action => string.Join(".", action.Action.ActionPath), StringComparer.Ordinal)
            .ToArray();
        var maxResults = request.Limit?.MaxResults ?? context.EffectiveResultLimit.MaxResults ?? 100;

        return CreateBoundedCollectionResult(
            context,
            ordered,
            maxResults,
            items => new CodeActionListData
            {
                Actions = items.Select(item => CreateInfo(item.Action, context, document, span, item.Descriptor)).ToArray(),
                ReturnedCount = items.Count,
                HasMore = items.Count < ordered.Length,
            });
    }

    public async ValueTask<PluginExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var resolvedAction = await ResolveActionAsync<DescribeCodeActionData>(request.ActionId, request.ExpectedSnapshot, expectedKind: null, context, cancellationToken).ConfigureAwait(false);
        if (resolvedAction.Rejection is not null)
        {
            return resolvedAction.Rejection;
        }

        var data = new DescribeCodeActionData
        {
            Descriptor = CreateInfo(resolvedAction.Action!, context, resolvedAction.Document!, resolvedAction.Span, resolvedAction.Descriptor!),
            Context = CreateContext(resolvedAction.Descriptor!),
        };

        return EnsureWithinSize(context, data);
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageCodeActionAsync(
        StageCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        return StageAsync(request.ActionId, request.ExpectedSnapshot, DiscoveredActionKind.Refactoring, context, cancellationToken);
    }

    public async ValueTask<PluginExecutionResult<MutationProposal>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshotRejection = ValidateSnapshot<MutationProposal>(context.WorkspaceResolver, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Location is null)
        {
            return Rejected<MutationProposal>("InvalidRequest", "A location selector is required.");
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken).ConfigureAwait(false);
        if (location.Status != SelectorResolveStatus.Resolved || location.Value is null)
        {
            return RejectFromStatus<MutationProposal>(location.Status, "Location");
        }

        var document = context.CurrentSolution.GetDocument(location.Value.SourceTree);
        if (document is null)
        {
            return Rejected<MutationProposal>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
        }

        var span = location.Value.SourceSpan;
        var matchingProviders = _refactoringProviders
            .Where(provider => string.IsNullOrWhiteSpace(request.ProviderId) || string.Equals(GetProviderId(provider), request.ProviderId, StringComparison.Ordinal))
            .OrderBy(GetProviderId, StringComparer.Ordinal)
            .ToArray();
        if (matchingProviders.Length == 0)
        {
            return Rejected<MutationProposal>("CodeActionUnavailable", "No matching refactoring provider is available.");
        }

        var candidates = new List<ClassifiedCodeAction>();
        foreach (var provider in matchingProviders)
        {
            var actions = await DiscoverRefactoringsAsync(provider, document, span, cancellationToken).ConfigureAwait(false);
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
            return Rejected<MutationProposal>("CodeActionUnavailable", "No matching replayable refactoring was available at the selected location.");
        }

        if (distinctCandidates.Length > 1)
        {
            return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "ActionAmbiguous",
                Message = "The requested refactoring could not be selected uniquely.",
            }, RequiredAction.ResolveTargetAgain);
        }

        var candidate = distinctCandidates[0];
        return candidate.Descriptor.ExecutionMode switch
        {
            CodeActionExecutionMode.Replay => await CreateMutationProposalAsync(candidate.Action.Action, candidate.Action.Title, context, cancellationToken).ConfigureAwait(false),
            CodeActionExecutionMode.Parameterised => PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            }),
            _ => Rejected<MutationProposal>("CodeActionUnavailable", "The selected action is not replayable in this server build.", RequiredAction.ResolveTargetAgain),
        };
    }

    public ValueTask<PluginExecutionResult<MutationProposal>> StageCodeFixAsync(
        StageCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        return StageAsync(request.ActionId, request.ExpectedSnapshot, DiscoveredActionKind.CodeFix, context, cancellationToken);
    }

    public async ValueTask<PluginExecutionResult<MutationProposal>> StageFixAllAsync(
        StageFixAllRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshotRejection = ValidateSnapshot<MutationProposal>(context.WorkspaceResolver, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Scope is null)
        {
            return Rejected<MutationProposal>("InvalidRequest", "A scope selector is required.");
        }

        if (!TryDecode(request.ActionId, out var payload))
        {
            return ActionExpired<MutationProposal>();
        }

        if (!string.Equals(payload.Kind, DiscoveredActionKind.CodeFix.ToString(), StringComparison.Ordinal))
        {
            return ActionExpired<MutationProposal>();
        }

        if (!DateTimeOffset.TryParse(payload.ExpiresAt, out var expiresAt) || expiresAt < DateTimeOffset.UtcNow)
        {
            return ActionExpired<MutationProposal>();
        }

        if (!string.Equals(payload.WorkspaceId, context.WorkspaceIdentity?.WorkspaceId, StringComparison.Ordinal)
            || payload.WorkspaceEpoch != (context.WorkspaceIdentity?.WorkspaceEpoch ?? 0)
            || payload.TransactionRevision != context.TransactionRevision)
        {
            return ActionExpired<MutationProposal>();
        }

        var originDocumentResolution = context.WorkspaceResolver.ResolveDocument(new DocumentSelector
        {
            Path = payload.DocumentPath,
        });
        if (originDocumentResolution.Status != SelectorResolveStatus.Resolved || originDocumentResolution.Value is null)
        {
            return ActionExpired<MutationProposal>();
        }

        var originDocument = originDocumentResolution.Value;
        var originSpan = new TextSpan(payload.Start, payload.Length);
        var provider = _codeFixProviders.SingleOrDefault(candidate => string.Equals(GetProviderId(candidate), payload.ProviderId, StringComparison.Ordinal));
        if (provider is null)
        {
            return FixAllUnavailable("The originating code-fix provider is no longer available.");
        }

        var fixAllProvider = provider.GetFixAllProvider();
        if (fixAllProvider is null)
        {
            return FixAllUnavailable("The selected code fix does not expose a fix-all provider.");
        }

        var diagnostics = await GetDocumentDiagnosticsAsync(originDocument, originSpan, payload.DiagnosticIds, cancellationToken);
        var actions = await DiscoverCodeFixesAsync(provider, originDocument, originSpan, diagnostics, cancellationToken);
        var matches = actions
            .Where(action =>
                string.Equals(action.Title, payload.Title, StringComparison.Ordinal)
                && string.Equals(action.EquivalenceKey, payload.EquivalenceKey, StringComparison.Ordinal)
                && action.ActionPath.SequenceEqual(payload.ActionPath)
                && action.DiagnosticIds.SequenceEqual(payload.DiagnosticIds, StringComparer.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
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
                        return ActionExpired<MutationProposal>();
                    }

                    var fixAllResult = await ApplyFixAllAsync(provider, fixAllProvider, workingOriginDocument, originSpan, FixAllScope.Solution, payload.DiagnosticIds, matches[0].EquivalenceKey, syntheticDiagnosticId: null, cancellationToken);
                    if (fixAllResult.Rejection is not null)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution!;
                    break;
                }

            case ScopeKind.Document:
                {
                    if (request.Scope.Document is null)
                    {
                        return Rejected<MutationProposal>("InvalidRequest", "Document scope requires a document selector.");
                    }

                    var documentResolution = context.WorkspaceResolver.ResolveDocument(request.Scope.Document);
                    if (documentResolution.Status != SelectorResolveStatus.Resolved || documentResolution.Value is null)
                    {
                        return RejectFromStatus<MutationProposal>(documentResolution.Status, "Document");
                    }

                    var targetDocument = workingSolution.GetDocument(documentResolution.Value.Id);
                    if (targetDocument is null)
                    {
                        return Rejected<MutationProposal>("DocumentNotFound", "The document selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
                    }

                    var fixAllResult = await ApplyFixAllAsync(provider, fixAllProvider, targetDocument, originSpan, FixAllScope.Document, payload.DiagnosticIds, matches[0].EquivalenceKey, syntheticDiagnosticId: null, cancellationToken);
                    if (fixAllResult.Rejection is not null)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution!;
                    break;
                }

            case ScopeKind.Project:
                {
                    if (request.Scope.Project is null)
                    {
                        return Rejected<MutationProposal>("InvalidRequest", "Project scope requires a project selector.");
                    }

                    var projectResolution = context.WorkspaceResolver.ResolveProject(request.Scope.Project);
                    if (projectResolution.Status != SelectorResolveStatus.Resolved || projectResolution.Value is null)
                    {
                        return RejectFromStatus<MutationProposal>(projectResolution.Status, "Project");
                    }

                    var targetProject = workingSolution.GetProject(projectResolution.Value.Id);
                    if (targetProject is null)
                    {
                        return Rejected<MutationProposal>("ProjectNotFound", "The project selector did not resolve to a source project.", RequiredAction.ResolveTargetAgain);
                    }

                    var fixAllResult = await ApplyFixAllAsync(provider, fixAllProvider, targetProject, payload.DiagnosticIds, matches[0].EquivalenceKey, syntheticDiagnosticId: null, cancellationToken);
                    if (fixAllResult.Rejection is not null)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution!;
                    break;
                }

            case ScopeKind.Projects:
                {
                    if (request.Scope.Projects is null || request.Scope.Projects.Count == 0)
                    {
                        return Rejected<MutationProposal>("InvalidRequest", "Projects scope requires at least one project selector.");
                    }

                    foreach (var projectSelector in request.Scope.Projects)
                    {
                        var projectResolution = context.WorkspaceResolver.ResolveProject(projectSelector);
                        if (projectResolution.Status != SelectorResolveStatus.Resolved || projectResolution.Value is null)
                        {
                            return RejectFromStatus<MutationProposal>(projectResolution.Status, "Project");
                        }

                        var targetProject = workingSolution.GetProject(projectResolution.Value.Id);
                        if (targetProject is null)
                        {
                            return Rejected<MutationProposal>("ProjectNotFound", "The project selector did not resolve to a source project.", RequiredAction.ResolveTargetAgain);
                        }

                        var fixAllResult = await ApplyFixAllAsync(provider, fixAllProvider, targetProject, payload.DiagnosticIds, matches[0].EquivalenceKey, syntheticDiagnosticId: null, cancellationToken);
                        if (fixAllResult.Rejection is not null)
                        {
                            return fixAllResult.Rejection;
                        }

                        workingSolution = fixAllResult.CandidateSolution!;
                    }

                    break;
                }

            default:
                return Rejected<MutationProposal>("InvalidRequest", "The requested scope kind is not supported for fix-all.");
        }

        var changedDocumentCount = await CountChangedSourceDocumentsAsync(context.CurrentSolution, workingSolution, cancellationToken);
        if (request.MaxChanges is int maxChanges && changedDocumentCount > maxChanges)
        {
            return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "FixAllLimitExceeded",
                Message = $"The fix-all operation would change {changedDocumentCount} source documents, exceeding the limit of {maxChanges}.",
            }, RequiredAction.NarrowRequest);
        }

        return PluginExecutionResult<MutationProposal>.Success(new MutationProposal
        {
            CandidateSolution = workingSolution,
            Summary = $"Fix all: {matches[0].Title}",
        });
    }

    public async ValueTask<PluginExecutionResult<MutationProposal>> StageScopedCodeFixAsync(
        ScopedCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshotRejection = ValidateSnapshot<MutationProposal>(context.WorkspaceResolver, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Scope is null)
        {
            return Rejected<MutationProposal>("InvalidRequest", "A scope selector is required.");
        }

        if (request.DiagnosticIds.Count == 0)
        {
            return Rejected<MutationProposal>("InvalidRequest", "At least one diagnostic ID is required.");
        }

        var documentsResolution = ResolveScopeDocuments(request.Scope, context);
        if (documentsResolution.Rejection is not null)
        {
            return documentsResolution.Rejection;
        }

        var matchingProviders = _codeFixProviders
            .Where(provider => string.IsNullOrWhiteSpace(request.ProviderId) || string.Equals(GetProviderId(provider), request.ProviderId, StringComparison.Ordinal))
            .OrderBy(GetProviderId, StringComparer.Ordinal)
            .ToArray();
        if (matchingProviders.Length == 0)
        {
            return Rejected<MutationProposal>("CodeFixUnavailable", "No matching code-fix provider is available.");
        }

        var candidates = new List<ScopedCodeFixCandidate>();
        var hadDiagnostics = false;

        foreach (var document in documentsResolution.Documents
            .OrderBy(document => context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var diagnostics = await GetScopedCodeFixDiagnosticsAsync(document, request.DiagnosticIds, request.AnalyzerTypeName, request.SyntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
            if (diagnostics.IsDefaultOrEmpty)
            {
                continue;
            }

            hadDiagnostics = true;
            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var documentSpan = new TextSpan(0, sourceText.Length);

            foreach (var provider in matchingProviders)
            {
                var actions = await DiscoverCodeFixesAsync(provider, document, documentSpan, diagnostics, cancellationToken).ConfigureAwait(false);
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
            return PluginExecutionResult<MutationProposal>.NoChange();
        }

        var distinctCandidates = candidates
            .GroupBy(candidate => new ScopedCodeFixCandidateKey
            {
                ProviderId = GetProviderId(candidate.Provider),
                Title = candidate.Title,
                EquivalenceKey = candidate.EquivalenceKey,
                DiagnosticIds = candidate.DiagnosticIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
            })
            .Select(static group => group.First())
            .ToArray();
        if (distinctCandidates.Length == 0)
        {
            return Rejected<MutationProposal>("CodeFixUnavailable", "No matching code fix was available for the selected scope.");
        }

        if (distinctCandidates.Length > 1)
        {
            return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
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
                        return Rejected<MutationProposal>("DocumentNotFound", "The selected scope could not be resolved to a source document.", RequiredAction.ResolveTargetAgain);
                    }

                    var fixAllResult = await ApplyFixAllAsync(candidate.Provider, fixAllProvider, originDocument, candidate.DocumentSpan, FixAllScope.Solution, candidate.DiagnosticIds, candidate.EquivalenceKey, request.SyntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
                    if (fixAllResult.Rejection is not null)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution!;
                    break;
                }

            case ScopeKind.Document:
                {
                    if (request.Scope.Document is null)
                    {
                        return Rejected<MutationProposal>("InvalidRequest", "Document scope requires a document selector.");
                    }

                    var documentResolution = context.WorkspaceResolver.ResolveDocument(request.Scope.Document);
                    if (documentResolution.Status != SelectorResolveStatus.Resolved || documentResolution.Value is null)
                    {
                        return RejectFromStatus<MutationProposal>(documentResolution.Status, "Document");
                    }

                    var targetDocument = workingSolution.GetDocument(documentResolution.Value.Id);
                    if (targetDocument is null)
                    {
                        return Rejected<MutationProposal>("DocumentNotFound", "The document selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
                    }

                    var fixAllProvider = candidate.Provider.GetFixAllProvider();
                    if (fixAllProvider is null)
                    {
                        var directResult = await ApplyDocumentScopedCodeFixAsync(candidate, targetDocument, context, request.AnalyzerTypeName, request.SyntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
                        if (directResult.Rejection is not null)
                        {
                            return directResult.Rejection;
                        }

                        workingSolution = directResult.CandidateSolution!;
                        break;
                    }

                    var fixAllResult = await ApplyFixAllAsync(candidate.Provider, fixAllProvider, targetDocument, candidate.DocumentSpan, FixAllScope.Document, candidate.DiagnosticIds, candidate.EquivalenceKey, request.SyntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
                    if (fixAllResult.Rejection is not null)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution!;
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
                        return Rejected<MutationProposal>("InvalidRequest", "Project scope requires a project selector.");
                    }

                    var projectResolution = context.WorkspaceResolver.ResolveProject(request.Scope.Project);
                    if (projectResolution.Status != SelectorResolveStatus.Resolved || projectResolution.Value is null)
                    {
                        return RejectFromStatus<MutationProposal>(projectResolution.Status, "Project");
                    }

                    var targetProject = workingSolution.GetProject(projectResolution.Value.Id);
                    if (targetProject is null)
                    {
                        return Rejected<MutationProposal>("ProjectNotFound", "The project selector did not resolve to a source project.", RequiredAction.ResolveTargetAgain);
                    }

                    var fixAllResult = await ApplyFixAllAsync(candidate.Provider, fixAllProvider, targetProject, candidate.DiagnosticIds, candidate.EquivalenceKey, request.SyntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
                    if (fixAllResult.Rejection is not null)
                    {
                        return fixAllResult.Rejection;
                    }

                    workingSolution = fixAllResult.CandidateSolution!;
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
                        return Rejected<MutationProposal>("InvalidRequest", "Projects scope requires at least one project selector.");
                    }

                    foreach (var projectSelector in request.Scope.Projects)
                    {
                        var projectResolution = context.WorkspaceResolver.ResolveProject(projectSelector);
                        if (projectResolution.Status != SelectorResolveStatus.Resolved || projectResolution.Value is null)
                        {
                            return RejectFromStatus<MutationProposal>(projectResolution.Status, "Project");
                        }

                        var targetProject = workingSolution.GetProject(projectResolution.Value.Id);
                        if (targetProject is null)
                        {
                            return Rejected<MutationProposal>("ProjectNotFound", "The project selector did not resolve to a source project.", RequiredAction.ResolveTargetAgain);
                        }

                        var fixAllResult = await ApplyFixAllAsync(candidate.Provider, fixAllProvider, targetProject, candidate.DiagnosticIds, candidate.EquivalenceKey, request.SyntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
                        if (fixAllResult.Rejection is not null)
                        {
                            return fixAllResult.Rejection;
                        }

                        workingSolution = fixAllResult.CandidateSolution!;
                    }

                    break;
                }

            default:
                return Rejected<MutationProposal>("InvalidRequest", "The requested scope kind is not supported for scoped code-fix staging.");
        }

        var changedDocumentCount = await CountChangedSourceDocumentsAsync(context.CurrentSolution, workingSolution, cancellationToken).ConfigureAwait(false);
        if (request.MaxChanges is int maxChanges && changedDocumentCount > maxChanges)
        {
            return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "FixAllLimitExceeded",
                Message = $"The fix-all operation would change {changedDocumentCount} source documents, exceeding the limit of {maxChanges}.",
            }, RequiredAction.NarrowRequest);
        }

        return PluginExecutionResult<MutationProposal>.Success(new MutationProposal
        {
            CandidateSolution = workingSolution,
            Summary = candidate.Title,
        });
    }

    public async ValueTask<PluginExecutionResult<MutationProposal>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshotRejection = ValidateSnapshot<MutationProposal>(context.WorkspaceResolver, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Location is null)
        {
            return Rejected<MutationProposal>("InvalidRequest", "A location selector is required.");
        }

        if (request.DiagnosticIds.Count == 0)
        {
            return Rejected<MutationProposal>("InvalidRequest", "At least one diagnostic ID is required.");
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken).ConfigureAwait(false);
        if (location.Status != SelectorResolveStatus.Resolved || location.Value is null)
        {
            return RejectFromStatus<MutationProposal>(location.Status, "Location");
        }

        var document = context.CurrentSolution.GetDocument(location.Value.SourceTree);
        if (document is null)
        {
            return Rejected<MutationProposal>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
        }

        var span = location.Value.SourceSpan;
        var matchingProviders = _codeFixProviders
            .Where(provider => string.IsNullOrWhiteSpace(request.ProviderId) || string.Equals(GetProviderId(provider), request.ProviderId, StringComparison.Ordinal))
            .OrderBy(GetProviderId, StringComparer.Ordinal)
            .ToArray();
        if (matchingProviders.Length == 0)
        {
            return Rejected<MutationProposal>("CodeFixUnavailable", "No matching code-fix provider is available.");
        }

        var diagnostics = await GetLocationScopedCodeFixDiagnosticsAsync(document, span, request.DiagnosticIds, request.AnalyzerTypeName, request.SyntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
        if (diagnostics.IsDefaultOrEmpty)
        {
            return Rejected<MutationProposal>("CodeFixUnavailable", "No matching code fix was available at the selected location.");
        }

        var candidates = new List<ClassifiedCodeAction>();
        foreach (var provider in matchingProviders)
        {
            var actions = await DiscoverCodeFixesAsync(provider, document, span, diagnostics, cancellationToken).ConfigureAwait(false);
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
            return Rejected<MutationProposal>("CodeFixUnavailable", "No matching code fix was available at the selected location.");
        }

        if (distinctCandidates.Length > 1)
        {
            return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "ActionAmbiguous",
                Message = "The requested code fix could not be selected uniquely.",
            }, RequiredAction.ResolveTargetAgain);
        }

        var candidate = distinctCandidates[0];
        return candidate.Descriptor.ExecutionMode switch
        {
            CodeActionExecutionMode.Replay => await CreateMutationProposalAsync(candidate.Action.Action, candidate.Action.Title, context, cancellationToken).ConfigureAwait(false),
            CodeActionExecutionMode.Parameterised => await CreateMutationProposalAsync(candidate.Action.Action, candidate.Action.Title, context, cancellationToken).ConfigureAwait(false),
            _ => Rejected<MutationProposal>("CodeFixUnavailable", "The selected action is not replayable in this server build.", RequiredAction.ResolveTargetAgain),
        };
    }

    private async ValueTask<DirectCodeFixResult> ApplyDocumentScopedCodeFixAsync(
        ScopedCodeFixCandidate candidate,
        Document targetDocument,
        IMutationContext context,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var diagnostics = await GetScopedCodeFixDiagnosticsAsync(targetDocument, candidate.DiagnosticIds, analyzerTypeName, syntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
        if (diagnostics.IsDefaultOrEmpty)
        {
            return new DirectCodeFixResult
            {
                Rejection = Rejected<MutationProposal>("CodeFixUnavailable", "No matching code fix was available for the selected scope."),
            };
        }

        var sourceText = await targetDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var documentSpan = new TextSpan(0, sourceText.Length);
        var discovered = await DiscoverCodeFixesAsync(candidate.Provider, targetDocument, documentSpan, diagnostics, cancellationToken).ConfigureAwait(false);
        var matches = discovered
            .Where(action => string.Equals(action.Title, candidate.Title, StringComparison.OrdinalIgnoreCase)
                && string.Equals(action.EquivalenceKey, candidate.EquivalenceKey, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            return new DirectCodeFixResult
            {
                Rejection = Rejected<MutationProposal>("CodeFixUnavailable", "No matching code fix was available for the selected scope."),
            };
        }

        if (matches.Length > 1)
        {
            return new DirectCodeFixResult
            {
                Rejection = PluginExecutionResult<MutationProposal>.Rejected(new ToolError
                {
                    Code = "ActionAmbiguous",
                    Message = "The requested code fix could not be selected uniquely.",
                }),
            };
        }

        var proposalResult = await CreateMutationProposalAsync(matches[0].Action, matches[0].Title, context, cancellationToken).ConfigureAwait(false);
        return proposalResult.Outcome == ToolOutcome.Succeeded
            ? new DirectCodeFixResult
            {
                CandidateSolution = proposalResult.Data!.CandidateSolution,
            }
            : new DirectCodeFixResult
            {
                Rejection = proposalResult,
            };
    }

    private static PluginExecutionResult<T>? ValidateSnapshot<T>(IWorkspaceResolver resolver, SnapshotPrecondition? expectedSnapshot)
    {
        var result = resolver.ValidateSnapshot(expectedSnapshot);
        return result.Kind == SnapshotMatchKind.Matched
            ? null
            : PluginExecutionResult<T>.Conflict(new ToolError
            {
                Code = "SnapshotMismatch",
                Message = "The request snapshot does not match the current workspace snapshot.",
            }, RequiredAction.ResolveTargetAgain);
    }

    private static PluginExecutionResult<T> RejectFromStatus<T>(SelectorResolveStatus status, string targetName)
    {
        return status switch
        {
            SelectorResolveStatus.Ambiguous => Rejected<T>($"{targetName}Ambiguous", $"The {targetName.ToLowerInvariant()} selector matched multiple results.", RequiredAction.ResolveTargetAgain),
            _ => Rejected<T>($"{targetName}NotFound", $"The {targetName.ToLowerInvariant()} selector did not match any result.", RequiredAction.ResolveTargetAgain),
        };
    }

    private static PluginExecutionResult<T> Rejected<T>(string code, string message, RequiredAction? requiredAction = null)
    {
        return PluginExecutionResult<T>.Rejected(new ToolError
        {
            Code = code,
            Message = message,
        }, requiredAction);
    }

    private static PluginExecutionResult<CodeActionListData> CreateBoundedCollectionResult(
        IQueryContext context,
        IReadOnlyList<ClassifiedCodeAction> actions,
        int maxResults,
        Func<IReadOnlyList<ClassifiedCodeAction>, CodeActionListData> createData)
    {
        var limitedCount = Math.Min(maxResults, actions.Count);

        for (var count = limitedCount; count >= 0; count--)
        {
            var items = count == actions.Count ? actions : actions.Take(count).ToArray();
            var data = createData(items);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _serializerOptions);

            if (bytes.Length <= context.MaxResponseBytes)
            {
                return PluginExecutionResult<CodeActionListData>.Success(data);
            }
        }

        return Rejected<CodeActionListData>("ResponseLimitExceeded", "The response exceeded the configured response size limit.", RequiredAction.NarrowRequest);
    }

    private static PluginExecutionResult<T> EnsureWithinSize<T>(IQueryContext context, T data)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, _serializerOptions);
        return bytes.Length <= context.MaxResponseBytes
            ? PluginExecutionResult<T>.Success(data)
            : Rejected<T>("ResponseLimitExceeded", "The response exceeded the configured response size limit.", RequiredAction.NarrowRequest);
    }

    private CodeActionInfo CreateInfo(DiscoveredCodeAction action, IToolExecutionContext context, Document document, TextSpan span, CodeActionDescriptorEntry descriptor)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(_tokenLifetime);
        return new CodeActionInfo
        {
            ActionId = Encode(new CodeActionTokenPayload
            {
                Kind = action.Kind.ToString(),
                ProviderId = action.ProviderId,
                Title = action.Title,
                EquivalenceKey = action.EquivalenceKey,
                ActionPath = action.ActionPath.ToArray(),
                DiagnosticIds = action.DiagnosticIds.ToArray(),
                WorkspaceId = context.WorkspaceIdentity?.WorkspaceId,
                WorkspaceEpoch = context.WorkspaceIdentity?.WorkspaceEpoch ?? 0,
                TransactionRevision = context.TransactionRevision,
                ExpiresAt = expiresAt.ToString("O"),
                DocumentPath = context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name),
                Start = span.Start,
                Length = span.Length,
            }),
            WorkspaceId = context.WorkspaceIdentity?.WorkspaceId,
            Title = action.Title,
            ProviderId = action.ProviderId,
            Kind = action.Kind == DiscoveredActionKind.Refactoring ? "Refactoring" : "CodeFix",
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = action.ActionPath,
            DiagnosticIds = action.DiagnosticIds,
            WorkspaceEpoch = context.WorkspaceIdentity?.WorkspaceEpoch ?? 0,
            TransactionRevision = context.TransactionRevision,
            ExpiresAt = expiresAt.ToString("O"),
            ExecutionMode = descriptor.ExecutionMode,
            ExecutorTool = descriptor.ExecutorTool,
            DescribeTool = descriptor.DescribeTool,
            UnsupportedReasonCode = descriptor.UnsupportedReasonCode,
            Requirements = descriptor.Requirements,
        };
    }

    private async ValueTask<PluginExecutionResult<MutationProposal>> StageAsync(
        string actionId,
        SnapshotPrecondition? expectedSnapshot,
        DiscoveredActionKind expectedKind,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        var resolvedAction = await ResolveActionAsync<MutationProposal>(actionId, expectedSnapshot, expectedKind, context, cancellationToken).ConfigureAwait(false);
        if (resolvedAction.Rejection is not null)
        {
            return resolvedAction.Rejection;
        }

        if (resolvedAction.Descriptor!.ExecutionMode == CodeActionExecutionMode.Parameterised)
        {
            return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            });
        }

        return await CreateMutationProposalAsync(resolvedAction.Action!.Action, resolvedAction.Action.Title, context, cancellationToken).ConfigureAwait(false);
    }

    private CodeActionDescriptorContext CreateContext(CodeActionDescriptorEntry descriptor)
    {
        return new CodeActionDescriptorContext
        {
            Kind = descriptor.ContextKind,
            Message = descriptor.Message,
        };
    }

    private async ValueTask<ResolvedAction<T>> ResolveActionAsync<T>(
        string actionId,
        SnapshotPrecondition? expectedSnapshot,
        DiscoveredActionKind? expectedKind,
        IToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshotRejection = ValidateSnapshot<T>(context.WorkspaceResolver, expectedSnapshot);
        if (snapshotRejection is not null)
        {
            return new ResolvedAction<T>
            {
                Rejection = snapshotRejection,
            };
        }

        if (!TryDecode(actionId, out var payload))
        {
            return new ResolvedAction<T>
            {
                Rejection = ActionExpired<T>(),
            };
        }

        if (!Enum.TryParse<DiscoveredActionKind>(payload.Kind, ignoreCase: false, out var actualKind))
        {
            return new ResolvedAction<T>
            {
                Rejection = ActionExpired<T>(),
            };
        }

        if (expectedKind is not null && actualKind != expectedKind.Value)
        {
            return new ResolvedAction<T>
            {
                Rejection = ActionExpired<T>(),
            };
        }

        if (!DateTimeOffset.TryParse(payload.ExpiresAt, out var expiresAt) || expiresAt < DateTimeOffset.UtcNow)
        {
            return new ResolvedAction<T>
            {
                Rejection = ActionExpired<T>(),
            };
        }

        if (!string.Equals(payload.WorkspaceId, context.WorkspaceIdentity?.WorkspaceId, StringComparison.Ordinal)
            || payload.WorkspaceEpoch != (context.WorkspaceIdentity?.WorkspaceEpoch ?? 0)
            || payload.TransactionRevision != context.TransactionRevision)
        {
            return new ResolvedAction<T>
            {
                Rejection = ActionExpired<T>(),
            };
        }

        var documentResolution = context.WorkspaceResolver.ResolveDocument(new DocumentSelector
        {
            Path = payload.DocumentPath,
        });
        if (documentResolution.Status != SelectorResolveStatus.Resolved || documentResolution.Value is null)
        {
            return new ResolvedAction<T>
            {
                Rejection = ActionExpired<T>(),
            };
        }

        var document = documentResolution.Value;
        var span = new TextSpan(payload.Start, payload.Length);
        var actions = actualKind == DiscoveredActionKind.Refactoring
            ? await DiscoverProviderActionsAsync(_refactoringProviders, payload.ProviderId, document, span, cancellationToken).ConfigureAwait(false)
            : await DiscoverProviderActionsAsync(
                _codeFixProviders,
                payload.ProviderId,
                document,
                span,
                await GetDocumentDiagnosticsAsync(document, span, payload.DiagnosticIds, cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        var matches = actions
            .Where(action =>
                string.Equals(action.Title, payload.Title, StringComparison.Ordinal)
                && string.Equals(action.EquivalenceKey, payload.EquivalenceKey, StringComparison.Ordinal)
                && action.ActionPath.SequenceEqual(payload.ActionPath)
                && action.DiagnosticIds.SequenceEqual(payload.DiagnosticIds, StringComparer.Ordinal))
            .ToArray();

        if (matches.Length != 1)
        {
            return new ResolvedAction<T>
            {
                Rejection = PluginExecutionResult<T>.Rejected(new ToolError
                {
                    Code = "ActionAmbiguous",
                    Message = "The requested action could not be reproduced uniquely.",
                }, RequiredAction.ResolveTargetAgain),
            };
        }

        var descriptor = _descriptorRegistry.Classify(matches[0].Action, matches[0].ProviderId, matches[0].Title);
        if (!descriptor.IsVisible)
        {
            return new ResolvedAction<T>
            {
                Rejection = Rejected<T>("ActionUnavailable", "The selected action is not available in this server build.", RequiredAction.ResolveTargetAgain),
            };
        }

        return new ResolvedAction<T>
        {
            Action = matches[0],
            Descriptor = descriptor,
            Document = document,
            Span = span,
        };
    }

    private static async Task<IReadOnlyList<DiscoveredCodeAction>> DiscoverProviderActionsAsync(
        IReadOnlyList<CodeRefactoringProvider> providers,
        string providerId,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var provider = providers.SingleOrDefault(candidate => string.Equals(GetProviderId(candidate), providerId, StringComparison.Ordinal));
        return provider is null
            ? []
            : await DiscoverRefactoringsAsync(provider, document, span, cancellationToken);
    }

    private static async Task<IReadOnlyList<DiscoveredCodeAction>> DiscoverProviderActionsAsync(
        IReadOnlyList<CodeFixProvider> providers,
        string providerId,
        Document document,
        TextSpan span,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var provider = providers.SingleOrDefault(candidate => string.Equals(GetProviderId(candidate), providerId, StringComparison.Ordinal));
        return provider is null
            ? []
            : await DiscoverCodeFixesAsync(provider, document, span, diagnostics, cancellationToken);
    }

    private static async Task<IReadOnlyList<DiscoveredCodeAction>> DiscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var rootActions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, span, action => rootActions.Add(action), cancellationToken);
        await provider.ComputeRefactoringsAsync(context);
        return Flatten(rootActions, GetProviderId(provider), DiscoveredActionKind.Refactoring, []);
    }

    private static async Task<IReadOnlyList<DiscoveredCodeAction>> DiscoverCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        TextSpan span,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var matchingDiagnostics = diagnostics
            .Where(diagnostic => provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToImmutableArray();
        if (matchingDiagnostics.IsDefaultOrEmpty)
        {
            return [];
        }

        var discovered = new List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)>();
        try
        {
            await RegisterCodeFixesAsync(provider, document, span, matchingDiagnostics, discovered, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            discovered.Clear();
            foreach (var diagnostic in matchingDiagnostics)
            {
                await RegisterCodeFixesAsync(provider, document, diagnostic.Location.SourceSpan, [diagnostic], discovered, cancellationToken).ConfigureAwait(false);
            }
        }

        return discovered
            .SelectMany(entry => Flatten([entry.Action], GetProviderId(provider), DiscoveredActionKind.CodeFix, entry.Diagnostics.Select(static diagnostic => diagnostic.Id).Distinct(StringComparer.Ordinal).ToArray()))
            .ToArray();
    }

    private static async Task RegisterCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        TextSpan requestedSpan,
        ImmutableArray<Diagnostic> diagnostics,
        ICollection<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)> discovered,
        CancellationToken cancellationToken)
    {
        var contextSpan = ExpandCodeFixContextSpan(requestedSpan, diagnostics);
        var context = new CodeFixContext(document, contextSpan, diagnostics, (action, actionDiagnostics) => discovered.Add((action, actionDiagnostics)), cancellationToken);
        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
    }

    private static TextSpan ExpandCodeFixContextSpan(TextSpan requestedSpan, ImmutableArray<Diagnostic> diagnostics)
    {
        var start = requestedSpan.Start;
        var end = requestedSpan.End;

        foreach (var diagnostic in diagnostics)
        {
            if (!diagnostic.Location.IsInSource)
            {
                continue;
            }

            start = Math.Min(start, diagnostic.Location.SourceSpan.Start);
            end = Math.Max(end, diagnostic.Location.SourceSpan.End);
        }

        return TextSpan.FromBounds(start, end);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, TextSpan span, IReadOnlyList<string>? diagnosticIds, CancellationToken cancellationToken)
    {
        return (await GetDocumentDiagnosticsAsync(document, diagnosticIds, cancellationToken))
            .Where(diagnostic => diagnostic.Location.SourceSpan.IntersectsWith(span))
            .ToImmutableArray();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, IReadOnlyList<string>? diagnosticIds, CancellationToken cancellationToken)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = compilation.GetDiagnostics(cancellationToken).ToList();
        var analyzers = document.Project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(document.Project.Language))
            .ToImmutableArray();
        if (!analyzers.IsDefaultOrEmpty)
        {
            diagnostics.AddRange(await compilation
                .WithAnalyzers(analyzers, document.Project.AnalyzerOptions)
                .GetAnalyzerDiagnosticsAsync(cancellationToken));
        }

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);

        return diagnostics
            .Where(diagnostic => diagnostic.Location.IsInSource && diagnostic.Location.SourceTree == syntaxTree)
            .Where(diagnostic => diagnosticIds is null || diagnosticIds.Count == 0 || diagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToImmutableArray();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetScopedCodeFixDiagnosticsAsync(
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var diagnostics = await GetDocumentDiagnosticsAsync(document, diagnosticIds, cancellationToken).ConfigureAwait(false);
        if (!diagnostics.IsDefaultOrEmpty)
        {
            return diagnostics;
        }

        diagnostics = await GetAdditionalAnalyzerDiagnosticsAsync(document, span: null, diagnosticIds, analyzerTypeName, cancellationToken).ConfigureAwait(false);
        if (!diagnostics.IsDefaultOrEmpty || string.IsNullOrWhiteSpace(syntheticDiagnosticId))
        {
            return diagnostics;
        }

        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var syntheticDiagnostic = await CreateSyntheticDiagnosticAsync(document, new TextSpan(0, sourceText.Length), syntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
        return syntheticDiagnostic is null ? [] : [syntheticDiagnostic];
    }

    private static async Task<ImmutableArray<Diagnostic>> GetLocationScopedCodeFixDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var diagnostics = await GetDocumentDiagnosticsAsync(document, span, diagnosticIds, cancellationToken).ConfigureAwait(false);
        if (!diagnostics.IsDefaultOrEmpty)
        {
            return diagnostics;
        }

        diagnostics = await GetAdditionalAnalyzerDiagnosticsAsync(document, span, diagnosticIds, analyzerTypeName, cancellationToken).ConfigureAwait(false);
        if (!diagnostics.IsDefaultOrEmpty || string.IsNullOrWhiteSpace(syntheticDiagnosticId))
        {
            return diagnostics;
        }

        var syntheticDiagnostic = await CreateSyntheticDiagnosticAsync(document, span, syntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
        return syntheticDiagnostic is null ? [] : [syntheticDiagnostic];
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAdditionalAnalyzerDiagnosticsAsync(
        Document document,
        TextSpan? span,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(analyzerTypeName))
        {
            return [];
        }

        var analyzer = CreateDiagnosticAnalyzer(analyzerTypeName);
        if (analyzer is null)
        {
            return [];
        }

        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return [];
        }

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxTree is null)
        {
            return [];
        }

        var diagnostics = await compilation
            .WithAnalyzers([analyzer], document.Project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken)
            .ConfigureAwait(false);

        return diagnostics
            .Where(diagnostic => diagnostic.Location.IsInSource && diagnostic.Location.SourceTree == syntaxTree)
            .Where(diagnostic => diagnosticIds.Count == 0 || diagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .Where(diagnostic => span is null || diagnostic.Location.SourceSpan.IntersectsWith(span.Value))
            .ToImmutableArray();
    }

    private static DiagnosticAnalyzer? CreateDiagnosticAnalyzer(string analyzerTypeName)
    {
        try
        {
            var analyzerType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(analyzerTypeName, throwOnError: false, ignoreCase: false))
                .FirstOrDefault(static candidate => candidate is not null);
            if (analyzerType is null || !typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzerType))
            {
                return null;
            }

            return Activator.CreateInstance(analyzerType, nonPublic: true) as DiagnosticAnalyzer;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<Diagnostic?> CreateSyntheticDiagnosticAsync(Document document, TextSpan span, string diagnosticId, CancellationToken cancellationToken)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxTree is null)
        {
            return null;
        }

        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            diagnosticId,
            diagnosticId,
            "Style",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden,
            isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, Location.Create(syntaxTree, span));
    }

    private static async Task<ImmutableArray<Diagnostic>> GetProjectDiagnosticsAsync(Project project, IReadOnlyList<string>? diagnosticIds, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = compilation.GetDiagnostics(cancellationToken).ToList();
        var analyzers = project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(project.Language))
            .ToImmutableArray();
        if (!analyzers.IsDefaultOrEmpty)
        {
            diagnostics.AddRange(await compilation
                .WithAnalyzers(analyzers, project.AnalyzerOptions)
                .GetAnalyzerDiagnosticsAsync(cancellationToken));
        }

        return diagnostics
            .Where(static diagnostic => !diagnostic.Location.IsInSource)
            .Where(diagnostic => diagnosticIds is null || diagnosticIds.Count == 0 || diagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToImmutableArray();
    }

    private static ScopeDocumentResolution ResolveScopeDocuments(ScopeSelector scope, IMutationContext context)
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
                Rejection = Rejected<MutationProposal>("InvalidRequest", "The requested scope kind is not supported."),
            },
        };
    }

    private static ScopeDocumentResolution ResolveDocumentScope(DocumentSelector? selector, IMutationContext context)
    {
        if (selector is null)
        {
            return new ScopeDocumentResolution
            {
                Rejection = Rejected<MutationProposal>("InvalidRequest", "Document scope requires a document selector."),
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
                Rejection = RejectFromStatus<MutationProposal>(resolution.Status, "Document"),
            };
    }

    private static ScopeDocumentResolution ResolveProjectScope(ProjectSelector? selector, IMutationContext context)
    {
        if (selector is null)
        {
            return new ScopeDocumentResolution
            {
                Rejection = Rejected<MutationProposal>("InvalidRequest", "Project scope requires a project selector."),
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
                Rejection = RejectFromStatus<MutationProposal>(resolution.Status, "Project"),
            };
    }

    private static ScopeDocumentResolution ResolveProjectsScope(IReadOnlyList<ProjectSelector>? selectors, IMutationContext context)
    {
        if (selectors is null || selectors.Count == 0)
        {
            return new ScopeDocumentResolution
            {
                Rejection = Rejected<MutationProposal>("InvalidRequest", "Projects scope requires at least one project selector."),
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
                    Rejection = RejectFromStatus<MutationProposal>(resolution.Status, "Project"),
                };
            }

            documents.AddRange(resolution.Value.Documents);
        }

        return new ScopeDocumentResolution
        {
            Documents = documents
                .DistinctBy(static document => document.Id)
                .ToArray(),
        };
    }

    private static async Task<int> CountChangedSourceDocumentsAsync(Solution before, Solution after, CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var document in before.Projects.SelectMany(static project => project.Documents))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var updatedDocument = after.GetDocument(document.Id);
            if (updatedDocument is null)
            {
                continue;
            }

            var originalText = await document.GetTextAsync(cancellationToken);
            var updatedText = await updatedDocument.GetTextAsync(cancellationToken);
            if (!originalText.ContentEquals(updatedText))
            {
                count++;
            }
        }

        return count;
    }

    private static PluginExecutionResult<MutationProposal> FixAllUnavailable(string message)
    {
        return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "FixAllUnavailable",
            Message = message,
        });
    }

    private async ValueTask<PluginExecutionResult<MutationProposal>> CreateMutationProposalAsync(
        CodeAction action,
        string summary,
        IToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var descriptor = _descriptorRegistry.Classify(action, string.Empty, action.Title);
        if (descriptor.ExecutionMode == CodeActionExecutionMode.Parameterised)
        {
            return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            });
        }

        var operations = await action.GetOperationsAsync(context.CurrentSolution, new Progress<CodeAnalysisProgress>(), cancellationToken).ConfigureAwait(false);
        if (!TryGetSupportedApplyChangesOperation(operations, out var applyChanges))
        {
            return PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "UnsupportedActionOperation",
                Message = "The selected action produced unsupported operations.",
            });
        }

        return PluginExecutionResult<MutationProposal>.Success(new MutationProposal
        {
            CandidateSolution = applyChanges!.ChangedSolution,
            Summary = summary,
        });
    }

    private static Task<FixAllApplyResult> ApplyFixAllAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        TextSpan originSpan,
        FixAllScope scope,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            document,
            scope is FixAllScope.ContainingMember or FixAllScope.ContainingType ? originSpan : null,
            provider,
            scope,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceDiagnosticProvider(diagnosticIds, syntheticDiagnosticId),
            cancellationToken);

        return ApplyFixAllCoreAsync(fixAllProvider, fixAllContext, cancellationToken);
    }

    private static Task<FixAllApplyResult> ApplyFixAllAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Project project,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            project,
            provider,
            FixAllScope.Project,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceDiagnosticProvider(diagnosticIds, syntheticDiagnosticId),
            cancellationToken);

        return ApplyFixAllCoreAsync(fixAllProvider, fixAllContext, cancellationToken);
    }

    private static async Task<FixAllApplyResult> ApplyFixAllCoreAsync(FixAllProvider fixAllProvider, FixAllContext fixAllContext, CancellationToken cancellationToken)
    {
        var fixAllAction = await fixAllProvider.GetFixAsync(fixAllContext);
        if (fixAllAction is null)
        {
            return new FixAllApplyResult
            {
                Rejection = FixAllUnavailable("The selected code fix could not produce a fix-all action."),
            };
        }

        var operations = await fixAllAction.GetOperationsAsync(fixAllContext.Solution, new Progress<CodeAnalysisProgress>(), cancellationToken);
        if (!TryGetSupportedApplyChangesOperation(operations, out var applyChanges))
        {
            return new FixAllApplyResult
            {
                Rejection = PluginExecutionResult<MutationProposal>.Rejected(new ToolError
                {
                    Code = "UnsupportedActionOperation",
                    Message = "The selected action produced unsupported operations.",
                }),
            };
        }

        return new FixAllApplyResult
        {
            CandidateSolution = applyChanges!.ChangedSolution,
        };
    }

    private static bool TryGetSupportedApplyChangesOperation(
        IReadOnlyList<CodeActionOperation> operations,
        out ApplyChangesOperation? applyChanges)
    {
        applyChanges = null;

        foreach (var operation in operations)
        {
            if (operation is ApplyChangesOperation candidate)
            {
                if (applyChanges is not null)
                {
                    applyChanges = null;
                    return false;
                }

                applyChanges = candidate;
                continue;
            }

            if (!IsIgnorableAuxiliaryOperation(operation))
            {
                applyChanges = null;
                return false;
            }
        }

        return applyChanges is not null;
    }

    private static bool IsIgnorableAuxiliaryOperation(CodeActionOperation operation)
    {
        return string.Equals(
            operation.GetType().FullName,
            "Microsoft.CodeAnalysis.Wrapping.WrapItemsAction+RecordCodeActionOperation",
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<DiscoveredCodeAction> Flatten(
        IReadOnlyList<CodeAction> rootActions,
        string providerId,
        DiscoveredActionKind kind,
        IReadOnlyList<string> diagnosticIds)
    {
        var discovered = new List<DiscoveredCodeAction>();
        for (var index = 0; index < rootActions.Count; index++)
        {
            FlattenCore(rootActions[index], providerId, kind, diagnosticIds, [index], discovered);
        }

        return discovered;
    }

    private static void FlattenCore(
        CodeAction action,
        string providerId,
        DiscoveredActionKind kind,
        IReadOnlyList<string> diagnosticIds,
        IReadOnlyList<int> path,
        ICollection<DiscoveredCodeAction> discovered)
    {
        var nested = action.NestedActions;
        if (!nested.IsDefaultOrEmpty)
        {
            for (var index = 0; index < nested.Length; index++)
            {
                var nestedPath = path.Concat([index]).ToArray();
                FlattenCore(nested[index], providerId, kind, diagnosticIds, nestedPath, discovered);
            }

            return;
        }

        discovered.Add(new DiscoveredCodeAction
        {
            Action = action,
            Kind = kind,
            ProviderId = providerId,
            Title = action.Title,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = path.ToArray(),
            DiagnosticIds = diagnosticIds.ToArray(),
        });
    }

    private static string GetProviderId(object provider)
    {
        return provider.GetType().FullName ?? provider.GetType().Name;
    }

    private string Encode(CodeActionTokenPayload payload)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, _serializerOptions);
        var signatureBytes = HMACSHA256.HashData(_secret, payloadBytes);
        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signatureBytes)}";
    }

    private bool TryDecode(string token, out CodeActionTokenPayload payload)
    {
        payload = new CodeActionTokenPayload();
        var separatorIndex = token.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
        {
            return false;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(token[..separatorIndex]);
            var signatureBytes = Base64UrlDecode(token[(separatorIndex + 1)..]);
            var expectedSignature = HMACSHA256.HashData(_secret, payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
            {
                return false;
            }

            var parsed = JsonSerializer.Deserialize<CodeActionTokenPayload>(payloadBytes, _serializerOptions);
            if (parsed is null)
            {
                return false;
            }

            payload = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        var remainder = padded.Length % 4;
        if (remainder > 0)
        {
            padded = padded.PadRight(padded.Length + (4 - remainder), '=');
        }

        return Convert.FromBase64String(padded);
    }

    private static PluginExecutionResult<T> ActionExpired<T>()
    {
        return PluginExecutionResult<T>.Rejected(new ToolError
        {
            Code = "ActionExpired",
            Message = "The requested action token is no longer valid.",
        }, RequiredAction.ResolveTargetAgain);
    }

    private sealed record CodeActionTokenPayload
    {
        public string Kind { get; init; } = string.Empty;

        public string ProviderId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<int> ActionPath { get; init; } = [];

        public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

        public string? WorkspaceId { get; init; }

        public long WorkspaceEpoch { get; init; }

        public int? TransactionRevision { get; init; }

        public string ExpiresAt { get; init; } = string.Empty;

        public string DocumentPath { get; init; } = string.Empty;

        public int Start { get; init; }

        public int Length { get; init; }
    }

    private sealed record DiscoveredCodeAction
    {
        public CodeAction Action { get; init; } = null!;

        public DiscoveredActionKind Kind { get; init; }

        public string ProviderId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<int> ActionPath { get; init; } = [];

        public IReadOnlyList<string> DiagnosticIds { get; init; } = [];
    }

    private sealed record ScopeDocumentResolution
    {
        public PluginExecutionResult<MutationProposal>? Rejection { get; init; }

        public IReadOnlyList<Document> Documents { get; init; } = [];
    }

    private sealed record ClassifiedCodeAction
    {
        public DiscoveredCodeAction Action { get; init; } = null!;

        public CodeActionDescriptorEntry Descriptor { get; init; } = null!;
    }

    private sealed record ScopedCodeFixCandidate
    {
        public Document Document { get; init; } = null!;

        public TextSpan DocumentSpan { get; init; }

        public CodeFixProvider Provider { get; init; } = null!;

        public string Title { get; init; } = string.Empty;

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<string> DiagnosticIds { get; init; } = [];
    }

    private sealed record ScopedCodeFixCandidateKey
    {
        public string ProviderId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<string> DiagnosticIds { get; init; } = [];
    }

    private sealed record ReplayCodeActionCandidateKey
    {
        public string ProviderId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<int> ActionPath { get; init; } = [];
    }

    private sealed record DirectCodeFixCandidateKey
    {
        public string ProviderId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<int> ActionPath { get; init; } = [];

        public IReadOnlyList<string> DiagnosticIds { get; init; } = [];
    }

    private sealed record FixAllApplyResult
    {
        public Solution? CandidateSolution { get; init; }

        public PluginExecutionResult<MutationProposal>? Rejection { get; init; }
    }

    private sealed record DirectCodeFixResult
    {
        public Solution? CandidateSolution { get; init; }

        public PluginExecutionResult<MutationProposal>? Rejection { get; init; }
    }

    private sealed record ResolvedAction<T>
    {
        public PluginExecutionResult<T>? Rejection { get; init; }

        public DiscoveredCodeAction? Action { get; init; }

        public CodeActionDescriptorEntry? Descriptor { get; init; }

        public Document? Document { get; init; }

        public TextSpan Span { get; init; }
    }

    private sealed class WorkspaceDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private readonly IReadOnlyList<string> _diagnosticIds;
        private readonly string? _syntheticDiagnosticId;

        public WorkspaceDiagnosticProvider(IReadOnlyList<string> diagnosticIds, string? syntheticDiagnosticId)
        {
            _diagnosticIds = diagnosticIds;
            _syntheticDiagnosticId = syntheticDiagnosticId;
        }

        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            return await MefCodeActionService.GetScopedCodeFixDiagnosticsAsync(document, _diagnosticIds, analyzerTypeName: null, syntheticDiagnosticId: _syntheticDiagnosticId, cancellationToken);
        }

        public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return await MefCodeActionService.GetProjectDiagnosticsAsync(project, _diagnosticIds, cancellationToken);
        }

        public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            var documentDiagnostics = new List<Diagnostic>();
            foreach (var document in project.Documents)
            {
                documentDiagnostics.AddRange(await MefCodeActionService.GetDocumentDiagnosticsAsync(document, _diagnosticIds, cancellationToken));
            }

            documentDiagnostics.AddRange(await MefCodeActionService.GetProjectDiagnosticsAsync(project, _diagnosticIds, cancellationToken));
            return documentDiagnostics;
        }
    }

    private enum DiscoveredActionKind
    {
        Refactoring,
        CodeFix,
    }
}
