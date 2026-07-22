using static Roslyn.Workbench.Mcp.CodeActions.Execution.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class ListCodeActionsTool : CodeActionQueryToolHandler<ListCodeActionsRequest, CodeActionListData>
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionInfoFactory _infoFactory;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public ListCodeActionsTool(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionInfoFactory infoFactory,
        ICodeActionToolRequestResolver requestResolver)
    {
        _providerCatalog = providerCatalog;
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
        if (!_providerCatalog.Status.IsAvailable)
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
            foreach (var provider in _discoveryService.GetMatchingRefactoringProviders(providerId: null))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var actions = await _discoveryService.DiscoverRefactoringsAsync(provider, document, span, cancellationToken);
                discovered.AddRange(actions);
            }
        }

        if (request.IncludeCodeFixes)
        {
            var codeFixProviders = _discoveryService.GetMatchingCodeFixProviders(providerId: null);
            if (codeFixProviders.Count > 0)
            {
                var diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(
                    document,
                    span,
                    request.DiagnosticIds,
                    cancellationToken);

                foreach (var provider in codeFixProviders)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var actions = await _discoveryService.DiscoverCodeFixesAsync(provider, document, diagnostics, cancellationToken);
                    discovered.AddRange(actions);
                }
            }
        }

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

        var actionInfos = new List<CodeActionInfo>();
        foreach (var action in visibleActions)
        {
            actionInfos.Add(_infoFactory.Create(action, context, document, span, action.Descriptor));
        }

        var data = new CodeActionListData
        {
            Actions = actionInfos,
        };

        return CodeActionExecutionResult<CodeActionListData>.Success(data);
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

        return left.ActionPath.Count.CompareTo(right.ActionPath.Count);
    }
}
