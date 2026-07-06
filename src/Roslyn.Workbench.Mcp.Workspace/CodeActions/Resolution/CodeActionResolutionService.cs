namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Resolution;

internal sealed class CodeActionResolutionService : ICodeActionResolutionService
{
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionDescriptorRegistry _descriptorRegistry;
    private readonly ICodeActionTokenService _tokenService;

    public CodeActionResolutionService(
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionDescriptorRegistry descriptorRegistry,
        ICodeActionTokenService tokenService)
    {
        _discoveryService = discoveryService;
        _diagnosticService = diagnosticService;
        _descriptorRegistry = descriptorRegistry;
        _tokenService = tokenService;
    }

    public async ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
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
            return new CodeActionResolution<T>
            {
                Rejection = snapshotRejection,
            };
        }

        if (!_tokenService.TryDecode(actionId, out var payload))
        {
            return RejectedResolution<T>(ActionExpired<T>());
        }

        if (!Enum.TryParse<DiscoveredActionKind>(payload.Kind, ignoreCase: false, out var actualKind))
        {
            return RejectedResolution<T>(ActionExpired<T>());
        }

        if (expectedKind is not null && actualKind != expectedKind.Value)
        {
            return RejectedResolution<T>(ActionExpired<T>());
        }

        if (!DateTimeOffset.TryParse(payload.ExpiresAt, out var expiresAt) || expiresAt < DateTimeOffset.UtcNow)
        {
            return RejectedResolution<T>(ActionExpired<T>());
        }

        if (!string.Equals(payload.WorkspaceId, context.WorkspaceIdentity.WorkspaceId, StringComparison.Ordinal)
            || payload.WorkspaceEpoch != context.WorkspaceIdentity.WorkspaceEpoch
            || payload.TransactionRevision != context.TransactionRevision)
        {
            return RejectedResolution<T>(ActionExpired<T>());
        }

        var documentResolution = context.WorkspaceResolver.ResolveDocument(new DocumentSelector
        {
            Path = payload.DocumentPath,
        });
        if (documentResolution.Status != SelectorResolveStatus.Resolved || documentResolution.Value is null)
        {
            return RejectedResolution<T>(ActionExpired<T>());
        }

        var document = documentResolution.Value;
        var span = new TextSpan(payload.Start, payload.Length);
        var actions = actualKind == DiscoveredActionKind.Refactoring
            ? await _discoveryService.DiscoverProviderRefactoringsAsync(payload.ProviderId, document, span, cancellationToken).ConfigureAwait(false)
            : await _discoveryService.DiscoverProviderCodeFixesAsync(
                payload.ProviderId,
                document,
                span,
                await _diagnosticService.GetDocumentDiagnosticsAsync(document, span, payload.DiagnosticIds, cancellationToken).ConfigureAwait(false),
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
            return RejectedResolution<T>(PluginExecutionResult<T>.Rejected(new ToolError
            {
                Code = "ActionAmbiguous",
                Message = "The requested action could not be reproduced uniquely.",
            }, RequiredAction.ResolveTargetAgain));
        }

        var descriptor = _descriptorRegistry.Classify(matches[0].Action, matches[0].ProviderId, matches[0].Title);
        if (!descriptor.IsVisible)
        {
            return RejectedResolution<T>(Rejected<T>(
                "ActionUnavailable",
                "The selected action is not available in this server build.",
                RequiredAction.ResolveTargetAgain));
        }

        return new CodeActionResolution<T>
        {
            Action = matches[0],
            Descriptor = descriptor,
            Document = document,
            Span = span,
        };
    }

    private static CodeActionResolution<T> RejectedResolution<T>(PluginExecutionResult<T> rejection)
    {
        return new CodeActionResolution<T>
        {
            Rejection = rejection,
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

    private static PluginExecutionResult<T> Rejected<T>(string code, string message, RequiredAction? requiredAction = null)
    {
        return PluginExecutionResult<T>.Rejected(new ToolError
        {
            Code = code,
            Message = message,
        }, requiredAction);
    }

    private static PluginExecutionResult<T> ActionExpired<T>()
    {
        return PluginExecutionResult<T>.Rejected(new ToolError
        {
            Code = "ActionExpired",
            Message = "The requested action token is no longer valid.",
        }, RequiredAction.ResolveTargetAgain);
    }
}
