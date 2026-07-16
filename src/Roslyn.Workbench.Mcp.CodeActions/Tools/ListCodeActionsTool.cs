using Roslyn.Workbench.Mcp.CodeActions.Contracts;
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

        var location = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken).ConfigureAwait(false);
        if (location.Status != SelectorResolveStatus.Resolved || location.Value is null)
        {
            return RejectFromStatus<CodeActionListData>(location.Status, "Location");
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
                var actions = await _discoveryService.DiscoverRefactoringsAsync(provider, document, span, cancellationToken).ConfigureAwait(false);
                discovered.AddRange(actions);
            }
        }

        if (request.IncludeCodeFixes)
        {
            var diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(
                document,
                span,
                request.DiagnosticIds,
                cancellationToken).ConfigureAwait(false);
            foreach (var provider in _discoveryService.GetMatchingCodeFixProviders(providerId: null))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var actions = await _discoveryService.DiscoverCodeFixesAsync(provider, document, diagnostics, cancellationToken).ConfigureAwait(false);
                discovered.AddRange(actions);
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
        return CodeActionExecutionResult<CodeActionListData>.Success(new CodeActionListData
        {
            Actions = ordered
                .Select(item => _infoFactory.Create(item.Action, context, document, span, item.Descriptor))
                .ToArray(),
        });
    }

    private sealed record ClassifiedCodeAction
    {
        public required DiscoveredCodeAction Action { get; init; }

        public required CodeActionDescriptorEntry Descriptor { get; init; }
    }
}
