using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

/// <summary>
/// Discovers available refactorings and Code Fixes for a selected source range.
/// </summary>
internal sealed class ListCodeActionsTool : CodeActionQueryToolHandler<ListCodeActionsRequest, CodeActionListData>
{
    private const string _toolName = "list-code-actions";
    private const int _warningLimit = 20;

    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionInfoFactory _infoFactory;
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionReferenceStore _referenceStore;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListCodeActionsTool"/> class.
    /// </summary>
    /// <param name="composition">The composed service set being inspected.</param>
    /// <param name="discoveryService">The service that provides discovery operations.</param>
    /// <param name="diagnosticService">The service used to obtain compiler diagnostics.</param>
    /// <param name="infoFactory">The factory used to create the required info.</param>
    /// <param name="providerCatalog">The catalogue used to locate Code Action providers.</param>
    /// <param name="referenceStore">The store that retains replayable references.</param>
    /// <param name="requestResolver">The resolver used to obtain request.</param>
    public ListCodeActionsTool(
        ICodeActionComposition composition,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionInfoFactory infoFactory,
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionReferenceStore referenceStore,
        ICodeActionToolRequestResolver requestResolver)
    {
        _composition = composition;
        _discoveryService = discoveryService;
        _diagnosticService = diagnosticService;
        _infoFactory = infoFactory;
        _providerCatalog = providerCatalog;
        _referenceStore = referenceStore;
        _requestResolver = requestResolver;
    }

    /// <inheritdoc/>
    protected override async ValueTask<CodeActionExecutionResult<CodeActionListData>> ExecuteCoreAsync(
        ListCodeActionsRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        if (!_composition.Status.IsAvailable)
        {
            return CodeActionsUnavailable<CodeActionListData>();
        }

        var snapshotRejection = _requestResolver.ValidateSnapshot<CodeActionListData>(
            context,
            request.ExpectedSnapshot);

        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        var selectionResolution = await _requestResolver.ResolveDocumentSelectionAsync<CodeActionListData>(
            request.Document,
            request.Range,
            context,
            cancellationToken);

        if (selectionResolution.HasRejection)
        {
            return selectionResolution.Rejection;
        }

        var document = selectionResolution.Value.Document;
        var span = selectionResolution.Value.Span;

        var discoveryResult = await DiscoverActionsAsync(
            request,
            document,
            span,
            cancellationToken);

        var (discovered, warnings) = discoveryResult;
        var result = await CreateResultAsync(
            discovered,
            document,
            request.EffectiveLimit,
            context,
            warnings,
            cancellationToken);

        return result;
    }

    private async ValueTask<(List<DiscoveredCodeAction> Actions, IReadOnlyList<WarningInfo> Warnings)> DiscoverActionsAsync(
        ListCodeActionsRequest request,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var actions = new List<DiscoveredCodeAction>();
        var warnings = new List<WarningInfo>();

        if (IncludesRefactorings(request.Kinds))
        {
            await AddDiscoveredRefactoringsAsync(actions, warnings, document, span, cancellationToken);
        }

        if (IncludesCodeFixes(request.Kinds))
        {
            await AddDiscoveredCodeFixesAsync(
                actions,
                warnings,
                request,
                document,
                span,
                cancellationToken);
        }

        return (actions, warnings);
    }

    private async ValueTask AddDiscoveredRefactoringsAsync(
        List<DiscoveredCodeAction> actions,
        List<WarningInfo> warnings,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            _toolName,
            WorkbenchPerformanceEventSource.RefactoringDiscoveryPhase))
        {
            var providers = _providerCatalog.GetMatchingRefactoringProviders(providerId: null);
            foreach (var provider in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var discovery = await _discoveryService.DiscoverRefactoringsAsync(
                    provider,
                    document,
                    span,
                    cancellationToken);

                if (!discovery.IsSuccessful)
                {
                    AddProviderWarning(warnings, discovery.Failure);
                    continue;
                }

                actions.AddRange(discovery.Value);
            }
        }
    }

    private async ValueTask AddDiscoveredCodeFixesAsync(
        List<DiscoveredCodeAction> actions,
        List<WarningInfo> warnings,
        ListCodeActionsRequest request,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var providers = _providerCatalog.GetMatchingCodeFixProviders(providerId: null);
        if (providers.Count == 0)
        {
            return;
        }

        var inspection = InspectCodeFixProviders(
            providers,
            request.DiagnosticIds,
            warnings,
            cancellationToken);
        var (providerMetadata, diagnosticIds) = inspection;
        if (diagnosticIds.Count == 0)
        {
            return;
        }

        var diagnosticCollection = await CollectDiagnosticsAsync(
            request,
            document,
            span,
            diagnosticIds,
            cancellationToken);

        AddDiagnosticWarnings(warnings, diagnosticCollection.Warnings);

        await AddCodeFixActionsAsync(
            actions,
            warnings,
            providerMetadata,
            document,
            diagnosticCollection.Diagnostics,
            cancellationToken);
    }

    private async ValueTask<CodeActionDiagnosticCollection> CollectDiagnosticsAsync(
        ListCodeActionsRequest request,
        Document document,
        TextSpan span,
        IReadOnlyList<string> diagnosticIds,
        CancellationToken cancellationToken)
    {
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            _toolName,
            WorkbenchPerformanceEventSource.DiagnosticCollectionPhase))
        {
            var diagnosticCollection = await _diagnosticService.CollectDocumentDiagnosticsAsync(
                document,
                request.Range is null ? null : span,
                diagnosticIds,
                cancellationToken);

            return diagnosticCollection;
        }
    }

    private async ValueTask AddCodeFixActionsAsync(
        List<DiscoveredCodeAction> actions,
        List<WarningInfo> warnings,
        IReadOnlyList<CodeFixProviderMetadata> providers,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            _toolName,
            WorkbenchPerformanceEventSource.CodeFixDiscoveryPhase))
        {
            foreach (var provider in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var discovery = await _discoveryService.DiscoverCodeFixesAsync(
                    provider,
                    document,
                    diagnostics,
                    cancellationToken);

                if (!discovery.IsSuccessful)
                {
                    AddProviderWarning(warnings, discovery.Failure);
                    continue;
                }

                actions.AddRange(discovery.Value);
            }
        }
    }

    private async ValueTask<CodeActionExecutionResult<CodeActionListData>> CreateResultAsync(
        List<DiscoveredCodeAction> discovered,
        Document document,
        int limit,
        ICodeActionQueryContext context,
        IReadOnlyList<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        var actionItems = new List<CodeActionListItem>(Math.Min(discovered.Count, limit));
        CodeActionExecutionResult<CodeActionListData>? result = null;
        try
        {
            var projection = await ProjectActionsAsync(
                discovered,
                document,
                limit,
                context,
                actionItems,
                cancellationToken);

            var (totalCount, failure) = projection;
            if (failure is not null)
            {
                result = failure;
                return result;
            }

            result = CreateSuccessResult(actionItems, totalCount, warnings);
            return result;
        }
        finally
        {
            if (result is null || !result.IsSucceeded)
            {
                RemovePublishedReferences(actionItems);
            }
        }
    }

    private async ValueTask<(int TotalCount, CodeActionExecutionResult<CodeActionListData>? Failure)> ProjectActionsAsync(
        List<DiscoveredCodeAction> discovered,
        Document document,
        int limit,
        ICodeActionQueryContext context,
        List<CodeActionListItem> actionItems,
        CancellationToken cancellationToken)
    {
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            _toolName,
            WorkbenchPerformanceEventSource.CodeActionProjectionPhase))
        {
            discovered.Sort(CompareActions);
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree is null)
            {
                return (0, null);
            }

            var totalCount = 0;
            foreach (var action in discovered)
            {
                var failure = ProjectAction(action, document, limit, context, syntaxTree, actionItems);
                if (failure is not null)
                {
                    return (totalCount, failure);
                }

                totalCount++;
            }

            return (totalCount, null);
        }
    }

    private CodeActionExecutionResult<CodeActionListData>? ProjectAction(
        DiscoveredCodeAction action,
        Document document,
        int limit,
        ICodeActionQueryContext context,
        SyntaxTree syntaxTree,
        List<CodeActionListItem> actionItems)
    {
        var sourceLocation = syntaxTree.GetLocation(action.TargetSpan);
        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
        if (resolvedLocation is null)
        {
            return CreateProjectionFault(
                "CodeActionLocationUnavailable",
                "A discovered Code Action location could not be projected into the workspace response.");
        }

        if (actionItems.Count == limit)
        {
            return null;
        }

        var creationResult = _infoFactory.Create(action, context, document, resolvedLocation);
        if (!creationResult.IsSucceeded)
        {
            return CreateProjectionFailure(creationResult.Status);
        }

        actionItems.Add(creationResult.Item);
        return null;
    }

    private void RemovePublishedReferences(IReadOnlyList<CodeActionListItem> actionItems)
    {
        foreach (var actionItem in actionItems)
        {
            _referenceStore.Remove(actionItem.ActionId);
        }
    }

    private (List<CodeFixProviderMetadata> Providers, List<string> DiagnosticIds) InspectCodeFixProviders(
        IReadOnlyList<CodeFixProvider> providers,
        IReadOnlyList<string>? requestedDiagnosticIds,
        List<WarningInfo> warnings,
        CancellationToken cancellationToken)
    {
        HashSet<string>? requestedDiagnosticIdSet = null;
        if (requestedDiagnosticIds is { Count: > 0 })
        {
            requestedDiagnosticIdSet = new HashSet<string>(requestedDiagnosticIds, StringComparer.Ordinal);
        }

        var availableProviders = new List<CodeFixProviderMetadata>(providers.Count);
        var effectiveDiagnosticIds = new List<string>();
        var seenDiagnosticIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            var inspection = _discoveryService.ReadCodeFixProviderMetadata(provider, cancellationToken);
            if (!inspection.IsSuccessful)
            {
                AddProviderWarning(warnings, inspection.Failure);
                continue;
            }

            availableProviders.Add(inspection.Value);
            foreach (var diagnosticId in inspection.Value.FixableDiagnosticIds)
            {
                if (requestedDiagnosticIdSet is not null && !requestedDiagnosticIdSet.Contains(diagnosticId))
                {
                    continue;
                }

                if (seenDiagnosticIds.Add(diagnosticId))
                {
                    effectiveDiagnosticIds.Add(diagnosticId);
                }
            }
        }

        return (availableProviders, effectiveDiagnosticIds);
    }

    private static CodeActionExecutionResult<CodeActionListData> CreateProjectionFailure(
        CodeActionInfoCreationStatus status)
    {
        return status switch
        {
            CodeActionInfoCreationStatus.LocationUnavailable => CreateProjectionFault(
                "CodeActionLocationUnavailable",
                "A discovered Code Action location was incomplete and could not be published."),
            CodeActionInfoCreationStatus.DocumentPathUnavailable => CreateProjectionFault(
                "CodeActionDocumentPathUnavailable",
                "A discovered Code Action document path could not be normalised for replay."),
            CodeActionInfoCreationStatus.ReferenceCapacityExceeded => Rejected<CodeActionListData>(
                "ActionReferenceCapacityExceeded",
                "The Code Action reference cache has reached its configured capacity. Retry after existing references expire, request fewer actions, or increase --code-action-reference-cache-size-limit."),
            _ => CreateProjectionFault(
                "CodeActionProjectionFailed",
                "A discovered Code Action could not be published because projection returned an unexpected result."),
        };
    }

    private static CodeActionExecutionResult<CodeActionListData> CreateSuccessResult(
        IReadOnlyList<CodeActionListItem> actionItems,
        int totalCount,
        IReadOnlyList<WarningInfo> warnings)
    {
        var boundedActions = BoundedCollection.CreatePrebounded(actionItems, totalCount);
        var data = new CodeActionListData
        {
            Actions = boundedActions,
        };

        return CodeActionExecutionResult.Success(data, warnings: warnings);
    }

    private static void AddProviderWarning(
        List<WarningInfo> warnings,
        CodeActionProviderFailure failure)
    {
        if (warnings.Count == _warningLimit)
        {
            return;
        }

        warnings.Add(new WarningInfo
        {
            Code = "CodeActionProviderWarning",
            Message = $"Code Action provider '{failure.ProviderId}' failed while {failure.Operation} ({failure.ExceptionType}).",
        });
    }

    private static void AddDiagnosticWarnings(
        List<WarningInfo> warnings,
        IReadOnlyList<string> diagnosticWarnings)
    {
        foreach (var diagnosticWarning in diagnosticWarnings)
        {
            if (warnings.Count == _warningLimit)
            {
                return;
            }

            warnings.Add(new WarningInfo
            {
                Code = "CodeActionDiagnosticWarning",
                Message = diagnosticWarning,
            });
        }
    }

    private static CodeActionExecutionResult<CodeActionListData> CreateProjectionFault(
        string code,
        string message)
    {
        var error = new CodeActionExecutionError
        {
            Code = code,
            Message = message,
        };

        return CodeActionExecutionResult.Faulted<CodeActionListData>(error);
    }

    private static bool IncludesCodeFixes(CodeActionKindSelection kinds)
    {
        return kinds is CodeActionKindSelection.CodeFixes or CodeActionKindSelection.All;
    }

    private static bool IncludesRefactorings(CodeActionKindSelection kinds)
    {
        return kinds is CodeActionKindSelection.Refactorings or CodeActionKindSelection.All;
    }

    private static int CompareActions(DiscoveredCodeAction left, DiscoveredCodeAction right)
    {
        var titleComparison = StringComparer.Ordinal.Compare(left.Title, right.Title);
        if (titleComparison != 0)
        {
            return titleComparison;
        }

        var providerComparison = StringComparer.Ordinal.Compare(left.ProviderId, right.ProviderId);
        if (providerComparison != 0)
        {
            return providerComparison;
        }

        var equivalenceKeyComparison = StringComparer.Ordinal.Compare(
            left.EquivalenceKey ?? string.Empty,
            right.EquivalenceKey ?? string.Empty);

        if (equivalenceKeyComparison != 0)
        {
            return equivalenceKeyComparison;
        }

        var actionPathComparison = CompareActionPaths(left.ActionPath, right.ActionPath);
        if (actionPathComparison != 0)
        {
            return actionPathComparison;
        }

        return CompareTargetSpans(left.TargetSpan, right.TargetSpan);
    }

    private static int CompareActionPaths(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        var sharedPathLength = Math.Min(left.Count, right.Count);
        for (var index = 0; index < sharedPathLength; index++)
        {
            var pathSegmentComparison = left[index].CompareTo(right[index]);
            if (pathSegmentComparison != 0)
            {
                return pathSegmentComparison;
            }
        }

        return left.Count.CompareTo(right.Count);
    }

    private static int CompareTargetSpans(TextSpan left, TextSpan right)
    {
        var spanStartComparison = left.Start.CompareTo(right.Start);
        if (spanStartComparison != 0)
        {
            return spanStartComparison;
        }

        return left.Length.CompareTo(right.Length);
    }
}
