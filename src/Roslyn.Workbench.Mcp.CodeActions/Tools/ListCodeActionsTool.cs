using static Roslyn.Workbench.Mcp.CodeActions.Execution.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class ListCodeActionsTool : CodeActionQueryToolHandler<ListCodeActionsRequest, CodeActionListData>
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionDescriptorRegistry _descriptorRegistry;
    private readonly ICodeActionInfoFactory _infoFactory;

    public ListCodeActionsTool(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionDescriptorRegistry descriptorRegistry,
        ICodeActionInfoFactory infoFactory)
    {
        _providerCatalog = providerCatalog;
        _discoveryService = discoveryService;
        _diagnosticService = diagnosticService;
        _descriptorRegistry = descriptorRegistry;
        _infoFactory = infoFactory;
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
            return RejectFromStatus<CodeActionListData>(location.Status, "Location", "location");
        }

        var document = context.CurrentSolution.GetDocument(location.Value.SourceTree);
        if (document is null)
        {
            return Rejected<CodeActionListData>(
                "LocationNotFound",
                "The location selector did not resolve to a source document.",
                RequiredAction.ResolveTargetAgain);
        }

        var span = location.Value.SourceSpan;
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
            var diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(
                document,
                span,
                request.DiagnosticIds,
                cancellationToken);

            foreach (var provider in _discoveryService.GetMatchingCodeFixProviders(providerId: null))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var actions = await _discoveryService.DiscoverCodeFixesAsync(provider, document, diagnostics, cancellationToken);
                discovered.AddRange(actions);
            }
        }

        var classifiedActions = new List<ClassifiedCodeAction>();
        foreach (var action in discovered)
        {
            var descriptor = _descriptorRegistry.Classify(action.Action, action.ProviderId, action.Title);
            if (!descriptor.IsVisible)
            {
                continue;
            }

            classifiedActions.Add(new ClassifiedCodeAction
            {
                Action = action,
                Descriptor = descriptor,
            });
        }

        var orderedActions = classifiedActions
            .OrderBy(static action => action.Action.Title, StringComparer.Ordinal)
            .ThenBy(static action => action.Action.ProviderId, StringComparer.Ordinal)
            .ThenBy(static action => action.Action.EquivalenceKey ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static action => string.Join(".", action.Action.ActionPath), StringComparer.Ordinal);

        var actionInfos = new List<CodeActionInfo>();
        foreach (var item in orderedActions)
        {
            actionInfos.Add(_infoFactory.Create(item.Action, context, document, span, item.Descriptor));
        }

        var data = new CodeActionListData
        {
            Actions = actionInfos,
        };

        return CodeActionExecutionResult<CodeActionListData>.Success(data);
    }

    private sealed record ClassifiedCodeAction
    {
        public required DiscoveredCodeAction Action { get; init; }

        public required CodeActionDescriptorEntry Descriptor { get; init; }
    }
}
