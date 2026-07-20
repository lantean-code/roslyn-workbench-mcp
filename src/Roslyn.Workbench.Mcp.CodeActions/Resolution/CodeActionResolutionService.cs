using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Roslyn.Workbench.Mcp.CodeActions.Resolution;

internal sealed class CodeActionResolutionService : ICodeActionResolutionService
{
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionTokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public CodeActionResolutionService(
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionTokenService tokenService,
        TimeProvider timeProvider)
    {
        _discoveryService = discoveryService;
        _diagnosticService = diagnosticService;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public async ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        string actionId,
        SnapshotPrecondition? expectedSnapshot,
        DiscoveredActionKind? expectedKind,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotRejection = CodeActionExecutionResultFactory.ValidateSnapshot<T>(
            context.WorkspaceResolver,
            expectedSnapshot);

        if (snapshotRejection is not null)
        {
            return RejectedResolution(snapshotRejection);
        }

        var tokenResolution = ResolveTokenContext(actionId, expectedKind, context);
        if (!tokenResolution.IsResolved)
        {
            return RejectedResolution(CodeActionExecutionResultFactory.ActionExpired<T>());
        }

        var rediscovery = await RediscoverActionsAsync(tokenResolution.Context, cancellationToken);
        if (!rediscovery.ProviderAvailable)
        {
            return RejectedResolution(
                ActionAmbiguous<T>(),
                CodeActionResolutionFailureKind.ProviderUnavailable);
        }

        var action = SelectUniqueAction(rediscovery.Actions, tokenResolution.Context.Payload);
        if (action is null)
        {
            return RejectedResolution(ActionAmbiguous<T>());
        }

        if (!action.Descriptor.IsVisible)
        {
            return RejectedResolution(CodeActionExecutionResultFactory.Rejected<T>(
                "ActionUnavailable",
                "The selected action is not available in this server build.",
                RequiredAction.ResolveTargetAgain));
        }

        return new CodeActionResolution<T>
        {
            Action = action,
            Descriptor = action.Descriptor,
            Document = tokenResolution.Context.Document,
            Span = tokenResolution.Context.Span,
        };
    }

    private CodeActionTokenContextResolution ResolveTokenContext(
        string actionId,
        DiscoveredActionKind? expectedKind,
        ICodeActionExecutionContext context)
    {
        if (!_tokenService.TryDecode(actionId, out var payload)
            || !Enum.TryParse<DiscoveredActionKind>(payload.Kind, ignoreCase: false, out var actualKind)
            || expectedKind is not null && actualKind != expectedKind.Value
            || !HasValidExpiry(payload)
            || !MatchesWorkspace(payload, context))
        {
            return new CodeActionTokenContextResolution();
        }

        var documentResolution = context.WorkspaceResolver.ResolveDocument(new DocumentSelector
        {
            Path = payload.DocumentPath,
        });

        if (documentResolution.Status != SelectorResolveStatus.Resolved || documentResolution.Value is null)
        {
            return new CodeActionTokenContextResolution();
        }

        return new CodeActionTokenContextResolution
        {
            Context = new CodeActionTokenContext
            {
                Payload = payload,
                Kind = actualKind,
                Document = documentResolution.Value,
                Span = new TextSpan(payload.Start, payload.Length),
            },
        };
    }

    private bool HasValidExpiry(CodeActionTokenPayload payload)
    {
        return DateTimeOffset.TryParseExact(
                payload.ExpiresAt,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAt)
            && expiresAt >= _timeProvider.GetUtcNow();
    }

    private static bool MatchesWorkspace(
        CodeActionTokenPayload payload,
        ICodeActionExecutionContext context)
    {
        return string.Equals(payload.WorkspaceId, context.WorkspaceIdentity.WorkspaceId, StringComparison.Ordinal)
            && payload.WorkspaceEpoch == context.WorkspaceIdentity.WorkspaceEpoch
            && payload.TransactionRevision == context.TransactionRevision;
    }

    private async ValueTask<CodeActionRediscovery> RediscoverActionsAsync(
        CodeActionTokenContext tokenContext,
        CancellationToken cancellationToken)
    {
        if (tokenContext.Kind == DiscoveredActionKind.Refactoring)
        {
            var providers = _discoveryService.GetMatchingRefactoringProviders(tokenContext.Payload.ProviderId);
            if (providers.Count != 1)
            {
                return new CodeActionRediscovery();
            }

            var refactorings = await _discoveryService.DiscoverRefactoringsAsync(
                providers[0],
                tokenContext.Document,
                tokenContext.Span,
                cancellationToken);

            return new CodeActionRediscovery
            {
                ProviderAvailable = true,
                Actions = refactorings,
            };
        }

        var codeFixProviders = _discoveryService.GetMatchingCodeFixProviders(tokenContext.Payload.ProviderId);
        if (codeFixProviders.Count != 1)
        {
            return new CodeActionRediscovery();
        }

        var diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(
            tokenContext.Document,
            tokenContext.Span,
            tokenContext.Payload.DiagnosticIds,
            cancellationToken);

        var codeFixes = await _discoveryService.DiscoverCodeFixesAsync(
            codeFixProviders[0],
            tokenContext.Document,
            diagnostics,
            cancellationToken);

        return new CodeActionRediscovery
        {
            ProviderAvailable = true,
            Actions = codeFixes,
        };
    }

    private static DiscoveredCodeAction? SelectUniqueAction(
        IReadOnlyList<DiscoveredCodeAction> actions,
        CodeActionTokenPayload payload)
    {
        DiscoveredCodeAction? matchingAction = null;
        foreach (var action in actions)
        {
            if (!string.Equals(action.Title, payload.Title, StringComparison.Ordinal)
                || !string.Equals(action.EquivalenceKey, payload.EquivalenceKey, StringComparison.Ordinal)
                || !action.ActionPath.SequenceEqual(payload.ActionPath)
                || !action.DiagnosticIds.SequenceEqual(payload.DiagnosticIds, StringComparer.Ordinal))
            {
                continue;
            }

            if (matchingAction is not null)
            {
                return null;
            }

            matchingAction = action;
        }

        return matchingAction;
    }

    private static CodeActionResolution<T> RejectedResolution<T>(
        CodeActionExecutionResult<T> rejection,
        CodeActionResolutionFailureKind failureKind = CodeActionResolutionFailureKind.None)
    {
        return new CodeActionResolution<T>
        {
            Rejection = rejection,
            FailureKind = failureKind,
        };
    }

    private static CodeActionExecutionResult<T> ActionAmbiguous<T>()
    {
        return CodeActionExecutionResultFactory.Rejected<T>(
            "ActionAmbiguous",
            "The requested action could not be reproduced uniquely.",
            RequiredAction.ResolveTargetAgain);
    }

    private sealed record CodeActionTokenContextResolution
    {
        public CodeActionTokenContext? Context { get; init; }

        [MemberNotNullWhen(true, nameof(Context))]
        public bool IsResolved => Context is not null;
    }

    private sealed record CodeActionTokenContext
    {
        public required CodeActionTokenPayload Payload { get; init; }

        public required DiscoveredActionKind Kind { get; init; }

        public required Document Document { get; init; }

        public required TextSpan Span { get; init; }
    }

    private sealed record CodeActionRediscovery
    {
        public bool ProviderAvailable { get; init; }

        public IReadOnlyList<DiscoveredCodeAction> Actions { get; init; } = [];
    }
}
