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

        var snapshotRejection = _requestResolver.ValidateSnapshot<CodeActionListData>(
            context,
            request.ExpectedSnapshot);

        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        var locationResolution = await _requestResolver.ResolveLocationAsync<CodeActionListData>(
            request.Location,
            context,
            cancellationToken);

        if (locationResolution.HasRejection)
        {
            return locationResolution.Rejection;
        }

        var document = locationResolution.Value.Document;
        var span = locationResolution.Value.Span;
        var discovered = new List<DiscoveredCodeAction>();
        if (request.IncludeRefactorings)
        {
            using (WorkbenchPerformanceEventSource.Log.StartPhase(
                _toolName,
                WorkbenchPerformanceEventSource.RefactoringDiscoveryPhase))
            {
                var refactoringProviders = _discoveryService.GetMatchingRefactoringProviders(providerId: null);
                foreach (var provider in refactoringProviders)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var actions = await _discoveryService.DiscoverRefactoringsAsync(provider, document, span, cancellationToken);
                    discovered.AddRange(actions);
                }
            }
        }

        if (request.IncludeCodeFixes)
        {
            var codeFixProviders = _discoveryService.GetMatchingCodeFixProviders(providerId: null);
            if (codeFixProviders.Count > 0)
            {
                var effectiveDiagnosticIds = GetEffectiveDiagnosticIds(codeFixProviders, request.DiagnosticIds);
                if (effectiveDiagnosticIds.Count > 0)
                {
                    IReadOnlyList<Diagnostic> diagnostics = [];
                    using (WorkbenchPerformanceEventSource.Log.StartPhase(
                        _toolName,
                        WorkbenchPerformanceEventSource.DiagnosticCollectionPhase))
                    {
                        diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(
                            document,
                            span,
                            effectiveDiagnosticIds,
                            cancellationToken);
                    }

                    using (WorkbenchPerformanceEventSource.Log.StartPhase(
                        _toolName,
                        WorkbenchPerformanceEventSource.CodeFixDiscoveryPhase))
                    {
                        foreach (var provider in codeFixProviders)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var actions = await _discoveryService.DiscoverCodeFixesAsync(provider, document, diagnostics, cancellationToken);
                            discovered.AddRange(actions);
                        }
                    }
                }
            }
        }

        List<CodeActionInfo> actionInfos;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            _toolName,
            WorkbenchPerformanceEventSource.CodeActionProjectionPhase))
        {
            var visibleActions = new List<DiscoveredCodeAction>();
            foreach (var action in discovered)
            {
                if (!action.Descriptor.IsVisible)
                {
                    continue;
                }

                visibleActions.Add(action);
            }

            visibleActions.Sort(CompareActions);

            actionInfos = new List<CodeActionInfo>();
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (syntaxTree is not null)
            {
                foreach (var action in visibleActions)
                {
                    var sourceLocation = syntaxTree.GetLocation(action.TargetSpan);
                    var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
                    if (resolvedLocation is null)
                    {
                        continue;
                    }

                    if (_infoFactory.TryCreate(
                        action,
                        context,
                        document,
                        resolvedLocation,
                        action.Descriptor,
                        out var actionInfo))
                    {
                        actionInfos.Add(actionInfo);
                    }
                }
            }
        }

        var data = new CodeActionListData
        {
            Actions = actionInfos,
        };

        return CodeActionExecutionResult.Success(data);
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
