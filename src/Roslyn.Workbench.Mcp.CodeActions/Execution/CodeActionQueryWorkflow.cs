using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionQueryWorkflow : ICodeActionQueryWorkflow
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionResolutionService _resolutionService;
    private readonly ICodeActionDescriptorRegistry _descriptorRegistry;
    private readonly ICodeActionTokenService _tokenService;
    private readonly TimeSpan _tokenLifetime;

    public CodeActionQueryWorkflow(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionResolutionService resolutionService,
        ICodeActionDescriptorRegistry descriptorRegistry,
        ICodeActionTokenService tokenService,
        IOptions<CodeActionExecutionOptions> options)
    {
        _providerCatalog = providerCatalog;
        _discoveryService = discoveryService;
        _diagnosticService = diagnosticService;
        _resolutionService = resolutionService;
        _descriptorRegistry = descriptorRegistry;
        _tokenService = tokenService;
        _tokenLifetime = options.Value.TokenLifetime;
    }

    public async ValueTask<CodeActionExecutionResult<CodeActionListData>> ListCodeActionsAsync(
        ListCodeActionsRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtimeRejection = RejectedIfUnavailable<CodeActionListData>();
        if (runtimeRejection is not null)
        {
            return runtimeRejection;
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
                discovered.AddRange(await _discoveryService.DiscoverRefactoringsAsync(provider, document, span, cancellationToken).ConfigureAwait(false));
            }
        }

        if (request.IncludeCodeFixes)
        {
            var diagnostics = await _diagnosticService
                .GetDocumentDiagnosticsAsync(document, span, request.DiagnosticIds, cancellationToken)
                .ConfigureAwait(false);
            foreach (var provider in _discoveryService.GetMatchingCodeFixProviders(providerId: null))
            {
                cancellationToken.ThrowIfCancellationRequested();
                discovered.AddRange(await _discoveryService.DiscoverCodeFixesAsync(provider, document, diagnostics, cancellationToken).ConfigureAwait(false));
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
                .Select(item => CreateInfo(item.Action, context, document, span, item.Descriptor))
                .ToArray(),
        });
    }

    public async ValueTask<CodeActionExecutionResult<DescribeCodeActionData>> DescribeCodeActionAsync(
        DescribeCodeActionRequest request,
        ICodeActionQueryContext context,
        CancellationToken cancellationToken)
    {
        var runtimeRejection = RejectedIfUnavailable<DescribeCodeActionData>();
        if (runtimeRejection is not null)
        {
            return runtimeRejection;
        }

        var resolvedAction = await _resolutionService.ResolveActionAsync<DescribeCodeActionData>(
            request.ActionId,
            request.ExpectedSnapshot,
            expectedKind: null,
            context,
            cancellationToken).ConfigureAwait(false);
        if (resolvedAction.HasRejection)
        {
            return resolvedAction.Rejection;
        }

        var data = new DescribeCodeActionData
        {
            Descriptor = CreateInfo(
                resolvedAction.Action,
                context,
                resolvedAction.Document,
                resolvedAction.Span,
                resolvedAction.Descriptor),
            Context = new CodeActionDescriptorContext
            {
                Kind = resolvedAction.Descriptor.ContextKind,
                Message = resolvedAction.Descriptor.Message,
            },
        };

        return CodeActionExecutionResult<DescribeCodeActionData>.Success(data);
    }

    private CodeActionInfo CreateInfo(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        TextSpan span,
        CodeActionDescriptorEntry descriptor)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(_tokenLifetime);

        return new CodeActionInfo
        {
            ActionId = _tokenService.Encode(new CodeActionTokenPayload
            {
                Kind = action.Kind.ToString(),
                ProviderId = action.ProviderId,
                Title = action.Title,
                EquivalenceKey = action.EquivalenceKey,
                ActionPath = action.ActionPath.ToArray(),
                DiagnosticIds = action.DiagnosticIds.ToArray(),
                WorkspaceId = context.WorkspaceIdentity.WorkspaceId,
                WorkspaceEpoch = context.WorkspaceIdentity.WorkspaceEpoch,
                TransactionRevision = context.TransactionRevision,
                ExpiresAt = expiresAt.ToString("O"),
                DocumentPath = context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name),
                Start = span.Start,
                Length = span.Length,
            }),
            WorkspaceId = context.WorkspaceIdentity.WorkspaceId,
            Title = action.Title,
            ProviderId = action.ProviderId,
            Kind = action.Kind == DiscoveredActionKind.Refactoring ? "Refactoring" : "CodeFix",
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = action.ActionPath,
            DiagnosticIds = action.DiagnosticIds,
            WorkspaceEpoch = context.WorkspaceIdentity.WorkspaceEpoch,
            TransactionRevision = context.TransactionRevision,
            ExpiresAt = expiresAt.ToString("O"),
            ExecutionMode = descriptor.ExecutionMode,
            ExecutorTool = descriptor.ExecutorTool,
            DescribeTool = descriptor.DescribeTool,
            UnsupportedReasonCode = descriptor.UnsupportedReasonCode,
            Requirements = descriptor.Requirements,
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

    private CodeActionExecutionResult<T>? RejectedIfUnavailable<T>()
    {
        return _providerCatalog.Status.IsAvailable
            ? null
            : Rejected<T>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }

    private sealed record ClassifiedCodeAction
    {
        public required DiscoveredCodeAction Action { get; init; }

        public required CodeActionDescriptorEntry Descriptor { get; init; }
    }
}
