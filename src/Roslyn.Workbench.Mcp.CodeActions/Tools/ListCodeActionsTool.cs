using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class ListCodeActionsTool : CodeActionQueryToolHandler<ListCodeActionsRequest, CodeActionListData>
{
    private const string _toolName = "list-code-actions";

    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionInfoFactory _infoFactory;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public ListCodeActionsTool(
        ICodeActionComposition composition,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionInfoFactory infoFactory,
        ICodeActionToolRequestResolver requestResolver)
    {
        _composition = composition;
        _discoveryService = discoveryService;
        _diagnosticService = diagnosticService;
        _infoFactory = infoFactory;
        _requestResolver = requestResolver;
    }

    protected override async ValueTask<CodeActionExecutionResult<CodeActionListData>> ExecuteCoreAsync(
        ListCodeActionsRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        if (!_composition.Status.IsAvailable)
        {
            return CodeActionsUnavailable<CodeActionListData>();
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

        var (discovered, diagnosticWarnings) = await DiscoverActionsAsync(
            request,
            document,
            span,
            cancellationToken);

        var boundedActions = await CreateBoundedActionsAsync(
            discovered,
            document,
            request.EffectiveLimit,
            context,
            cancellationToken);

        var data = new CodeActionListData
        {
            Actions = boundedActions,
        };

        var warnings = CreateWarnings(diagnosticWarnings);
        return CodeActionExecutionResult.Success(data, warnings: warnings);
    }

    private async ValueTask<(
        List<DiscoveredCodeAction> Actions,
        IReadOnlyList<string> DiagnosticWarnings)> DiscoverActionsAsync(
        ListCodeActionsRequest request,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var actions = new List<DiscoveredCodeAction>();
        IReadOnlyList<string> diagnosticWarnings = [];

        if (IncludesRefactorings(request.Kinds))
        {
            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                _toolName,
                WorkbenchPerformanceEventSource.RefactoringDiscoveryPhase))
            {
                var refactoringProviders = _discoveryService.GetMatchingRefactoringProviders(providerId: null);
                foreach (var provider in refactoringProviders)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var discovered = await _discoveryService.DiscoverRefactoringsAsync(
                        provider,
                        document,
                        span,
                        cancellationToken);

                    actions.AddRange(discovered);
                }
            }
        }

        if (IncludesCodeFixes(request.Kinds))
        {
            var codeFixProviders = _discoveryService.GetMatchingCodeFixProviders(providerId: null);
            if (codeFixProviders.Count > 0)
            {
                var effectiveDiagnosticIds = GetEffectiveDiagnosticIds(
                    codeFixProviders,
                    request.DiagnosticIds);

                if (effectiveDiagnosticIds.Count > 0)
                {
                    IReadOnlyList<Diagnostic> diagnostics = [];
                    using (WorkbenchPerformanceEventSource.Log.StartPhase(
                        _toolName,
                        WorkbenchPerformanceEventSource.DiagnosticCollectionPhase))
                    {
                        var diagnosticCollection = await _diagnosticService.CollectDocumentDiagnosticsAsync(
                            document,
                            request.Range is null ? null : span,
                            effectiveDiagnosticIds,
                            cancellationToken);

                        diagnostics = diagnosticCollection.Diagnostics;
                        diagnosticWarnings = diagnosticCollection.Warnings;
                    }

                    using (WorkbenchPerformanceEventSource.Log.StartPhase(
                        _toolName,
                        WorkbenchPerformanceEventSource.CodeFixDiscoveryPhase))
                    {
                        foreach (var provider in codeFixProviders)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var discovered = await _discoveryService.DiscoverCodeFixesAsync(
                                provider,
                                document,
                                diagnostics,
                                cancellationToken);

                            actions.AddRange(discovered);
                        }
                    }
                }
            }
        }

        return (actions, diagnosticWarnings);
    }

    private async ValueTask<BoundedCollection<CodeActionListItem>> CreateBoundedActionsAsync(
        List<DiscoveredCodeAction> discovered,
        Document document,
        int limit,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        List<CodeActionListItem> actionItems;
        var totalCount = 0;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            _toolName,
            WorkbenchPerformanceEventSource.CodeActionProjectionPhase))
        {
            discovered.Sort(CompareActions);
            actionItems = new List<CodeActionListItem>(Math.Min(discovered.Count, limit));
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree is not null)
            {
                foreach (var action in discovered)
                {
                    var sourceLocation = syntaxTree.GetLocation(action.TargetSpan);
                    var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
                    if (resolvedLocation is null)
                    {
                        continue;
                    }

                    if (actionItems.Count == limit)
                    {
                        totalCount++;
                        continue;
                    }

                    if (_infoFactory.TryCreate(
                        action,
                        context,
                        document,
                        resolvedLocation,
                        out var actionItem))
                    {
                        actionItems.Add(actionItem);
                        totalCount++;
                    }
                }
            }
        }

        return BoundedCollection.CreatePrebounded(actionItems, totalCount);
    }

    private static List<WarningInfo> CreateWarnings(
        IReadOnlyList<string> diagnosticWarnings)
    {
        var warnings = new List<WarningInfo>(diagnosticWarnings.Count);
        foreach (var warning in diagnosticWarnings)
        {
            warnings.Add(new WarningInfo
            {
                Code = "CodeActionDiagnosticWarning",
                Message = warning,
            });
        }

        return warnings;
    }

    private static bool IncludesCodeFixes(CodeActionKindSelection kinds)
    {
        return kinds is CodeActionKindSelection.CodeFixes or CodeActionKindSelection.All;
    }

    private static bool IncludesRefactorings(CodeActionKindSelection kinds)
    {
        return kinds is CodeActionKindSelection.Refactorings or CodeActionKindSelection.All;
    }

    private static List<string> GetEffectiveDiagnosticIds(
        IReadOnlyList<CodeFixProvider> providers,
        IReadOnlyList<string>? requestedDiagnosticIds)
    {
        HashSet<string>? requestedDiagnosticIdSet = null;
        if (requestedDiagnosticIds is { Count: > 0 })
        {
            requestedDiagnosticIdSet = new HashSet<string>(requestedDiagnosticIds, StringComparer.Ordinal);
        }

        var effectiveDiagnosticIds = new List<string>();
        var seenDiagnosticIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            foreach (var diagnosticId in provider.FixableDiagnosticIds)
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

        return effectiveDiagnosticIds;
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

        var sharedPathLength = Math.Min(left.ActionPath.Count, right.ActionPath.Count);
        for (var index = 0; index < sharedPathLength; index++)
        {
            var pathSegmentComparison = left.ActionPath[index].CompareTo(right.ActionPath[index]);
            if (pathSegmentComparison != 0)
            {
                return pathSegmentComparison;
            }
        }

        var pathLengthComparison = left.ActionPath.Count.CompareTo(right.ActionPath.Count);
        if (pathLengthComparison != 0)
        {
            return pathLengthComparison;
        }

        var spanStartComparison = left.TargetSpan.Start.CompareTo(right.TargetSpan.Start);
        if (spanStartComparison != 0)
        {
            return spanStartComparison;
        }

        return left.TargetSpan.Length.CompareTo(right.TargetSpan.Length);
    }
}
