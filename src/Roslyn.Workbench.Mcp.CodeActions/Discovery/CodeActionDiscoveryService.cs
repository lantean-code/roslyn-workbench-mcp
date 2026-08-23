using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionDiscoveryService : ICodeActionDiscoveryService
{
    private readonly ICodeActionPolicy _policy;

    public CodeActionDiscoveryService(ICodeActionPolicy policy)
    {
        _policy = policy;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Code Action providers are trusted external extensions; one provider failure must not suppress actions from unrelated providers.")]
    public CodeActionProviderInvocationResult<CodeFixProviderMetadata> ReadCodeFixProviderMetadata(
        CodeFixProvider provider,
        CancellationToken cancellationToken)
    {
        var providerId = CodeActionProviderIdentity.GetId(provider);
        try
        {
            var fixableDiagnosticIds = provider.FixableDiagnosticIds;
            if (fixableDiagnosticIds.IsDefault)
            {
                fixableDiagnosticIds = [];
            }

            var metadata = new CodeFixProviderMetadata
            {
                Provider = provider,
                FixableDiagnosticIds = fixableDiagnosticIds,
            };

            return CodeActionProviderInvocationResult.Success(metadata);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ProviderFailure<CodeFixProviderMetadata>(
                providerId,
                "reading fixable diagnostic IDs",
                exception);
        }
    }

    public async ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> DiscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        return await DiscoverRefactoringsCoreAsync(
            provider,
            document,
            span,
            enforcePolicy: true,
            cancellationToken);
    }

    public async ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> DiscoverCodeFixesAsync(
        CodeFixProviderMetadata providerMetadata,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        return await DiscoverCodeFixesCoreAsync(
            providerMetadata,
            document,
            diagnostics,
            enforcePolicy: true,
            cancellationToken);
    }

    public async ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> RediscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        return await DiscoverRefactoringsCoreAsync(
            provider,
            document,
            span,
            enforcePolicy: false,
            cancellationToken);
    }

    public async ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> RediscoverCodeFixesAsync(
        CodeFixProviderMetadata providerMetadata,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        return await DiscoverCodeFixesCoreAsync(
            providerMetadata,
            document,
            diagnostics,
            enforcePolicy: false,
            cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Code Action providers are trusted external extensions; one provider failure must not suppress actions from unrelated providers.")]
    private async ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> DiscoverRefactoringsCoreAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        bool enforcePolicy,
        CancellationToken cancellationToken)
    {
        var providerId = CodeActionProviderIdentity.GetId(provider);
        if (enforcePolicy && !_policy.EvaluateProvider(providerId).IsAllowed)
        {
            return CodeActionProviderInvocationResult.Success<IReadOnlyList<DiscoveredCodeAction>>([]);
        }

        var rootActions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, span, action => rootActions.Add(action), cancellationToken);
        try
        {
            await provider.ComputeRefactoringsAsync(context);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ProviderFailure<IReadOnlyList<DiscoveredCodeAction>>(
                providerId,
                "computing refactorings",
                exception);
        }

        return FlattenRefactorings(
            rootActions,
            providerId,
            span,
            enforcePolicy,
            cancellationToken);
    }

    private async ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> DiscoverCodeFixesCoreAsync(
        CodeFixProviderMetadata providerMetadata,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        bool enforcePolicy,
        CancellationToken cancellationToken)
    {
        var provider = providerMetadata.Provider;
        var providerId = CodeActionProviderIdentity.GetId(provider);
        if (enforcePolicy && !_policy.EvaluateProvider(providerId).IsAllowed)
        {
            return CodeActionProviderInvocationResult.Success<IReadOnlyList<DiscoveredCodeAction>>([]);
        }

        var diagnosticsBySpan = new Dictionary<TextSpan, List<Diagnostic>>();
        var orderedSpans = new List<TextSpan>();
        var fixableDiagnosticIds = providerMetadata.FixableDiagnosticIds;
        foreach (var diagnostic in diagnostics)
        {
            if (!IsFixableDiagnostic(fixableDiagnosticIds, diagnostic.Id))
            {
                continue;
            }

            var diagnosticSpan = diagnostic.Location.SourceSpan;
            if (!diagnosticsBySpan.TryGetValue(diagnosticSpan, out var diagnosticsAtSpan))
            {
                diagnosticsAtSpan = [];
                diagnosticsBySpan.Add(diagnosticSpan, diagnosticsAtSpan);
                orderedSpans.Add(diagnosticSpan);
            }

            diagnosticsAtSpan.Add(diagnostic);
        }

        if (orderedSpans.Count == 0)
        {
            return CodeActionProviderInvocationResult.Success<IReadOnlyList<DiscoveredCodeAction>>([]);
        }

        var registeredActions = new List<RegisteredCodeFix>();
        foreach (var diagnosticSpan in orderedSpans)
        {
            var groupedDiagnostics = diagnosticsBySpan[diagnosticSpan].ToImmutableArray();

            var registration = await RegisterCodeFixesAsync(
                provider,
                providerId,
                document,
                diagnosticSpan,
                groupedDiagnostics,
                cancellationToken);

            if (!registration.IsSuccessful)
            {
                return CodeActionProviderInvocationResult.Failed<IReadOnlyList<DiscoveredCodeAction>>(registration.Failure);
            }

            registeredActions.AddRange(registration.Value);
        }

        var fixAllScopeResult = GetFixAllScopes(provider, providerId, cancellationToken);
        if (!fixAllScopeResult.IsSuccessful)
        {
            return CodeActionProviderInvocationResult.Failed<IReadOnlyList<DiscoveredCodeAction>>(fixAllScopeResult.Failure);
        }

        return FlattenCodeFixes(
            registeredActions,
            fixAllScopeResult.Value,
            providerId,
            enforcePolicy,
            cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Code Action providers are trusted external extensions; one provider failure must not suppress actions from unrelated providers.")]
    private CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>> FlattenRefactorings(
        List<CodeAction> rootActions,
        string providerId,
        TextSpan span,
        bool enforcePolicy,
        CancellationToken cancellationToken)
    {
        try
        {
            var actions = Flatten(
                rootActions,
                providerId,
                DiscoveredActionKind.Refactoring,
                span,
                diagnosticIds: [],
                diagnostics: [],
                fixAllScopes: [],
                enforcePolicy);

            return CodeActionProviderInvocationResult.Success<IReadOnlyList<DiscoveredCodeAction>>(actions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ProviderFailure<IReadOnlyList<DiscoveredCodeAction>>(
                providerId,
                "projecting refactorings",
                exception);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Code Action providers are trusted external extensions; one provider failure must not suppress actions from unrelated providers.")]
    private CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>> FlattenCodeFixes(
        IReadOnlyList<RegisteredCodeFix> registeredActions,
        IReadOnlyList<CodeActionFixAllScope> fixAllScopes,
        string providerId,
        bool enforcePolicy,
        CancellationToken cancellationToken)
    {
        try
        {
            var discoveredActions = new List<DiscoveredCodeAction>();
            foreach (var registeredAction in registeredActions)
            {
                var action = registeredAction.Action;
                var actionDiagnostics = registeredAction.Diagnostics;
                var diagnosticIds = GetDistinctDiagnosticIds(actionDiagnostics);
                var diagnosticIdentities = GetDiagnosticIdentities(actionDiagnostics);
                IReadOnlyList<CodeActionFixAllScope> actionFixAllScopes = [];
                if (!string.IsNullOrWhiteSpace(action.EquivalenceKey))
                {
                    actionFixAllScopes = fixAllScopes;
                }

                FlattenCore(
                    action,
                    providerId,
                    DiscoveredActionKind.CodeFix,
                    registeredAction.TargetSpan,
                    diagnosticIds,
                    diagnosticIdentities,
                    actionFixAllScopes,
                    [registeredAction.RootIndex],
                    discoveredActions,
                    enforcePolicy);
            }

            return CodeActionProviderInvocationResult.Success<IReadOnlyList<DiscoveredCodeAction>>(discoveredActions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ProviderFailure<IReadOnlyList<DiscoveredCodeAction>>(
                providerId,
                "projecting code fixes",
                exception);
        }
    }

    private List<DiscoveredCodeAction> Flatten(
        List<CodeAction> rootActions,
        string providerId,
        DiscoveredActionKind kind,
        TextSpan targetSpan,
        IReadOnlyList<string> diagnosticIds,
        IReadOnlyList<CodeActionDiagnosticIdentity> diagnostics,
        IReadOnlyList<CodeActionFixAllScope> fixAllScopes,
        bool enforcePolicy)
    {
        var discovered = new List<DiscoveredCodeAction>();
        var path = new List<int>();

        for (var index = 0; index < rootActions.Count; index++)
        {
            path.Add(index);
            FlattenCore(
                rootActions[index],
                providerId,
                kind,
                targetSpan,
                diagnosticIds,
                diagnostics,
                fixAllScopes,
                path,
                discovered,
                enforcePolicy);

            path.RemoveAt(path.Count - 1);
        }

        return discovered;
    }

    private void FlattenCore(
        CodeAction action,
        string providerId,
        DiscoveredActionKind kind,
        TextSpan targetSpan,
        IReadOnlyList<string> diagnosticIds,
        IReadOnlyList<CodeActionDiagnosticIdentity> diagnostics,
        IReadOnlyList<CodeActionFixAllScope> fixAllScopes,
        List<int> path,
        ICollection<DiscoveredCodeAction> discovered,
        bool enforcePolicy)
    {
        var nested = action.NestedActions;
        if (!nested.IsDefaultOrEmpty)
        {
            for (var index = 0; index < nested.Length; index++)
            {
                path.Add(index);
                FlattenCore(
                    nested[index],
                    providerId,
                    kind,
                    targetSpan,
                    diagnosticIds,
                    diagnostics,
                    fixAllScopes,
                    path,
                    discovered,
                    enforcePolicy);

                path.RemoveAt(path.Count - 1);
            }

            return;
        }

        if (enforcePolicy && !_policy.EvaluateAction(providerId, action).IsAllowed)
        {
            return;
        }

        discovered.Add(new DiscoveredCodeAction
        {
            Action = action,
            Kind = kind,
            ProviderId = providerId,
            Title = action.Title,
            TargetSpan = targetSpan,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = path.ToArray(),
            DiagnosticIds = diagnosticIds,
            Diagnostics = diagnostics,
            FixAllScopes = fixAllScopes,
        });
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Code Action providers are trusted external extensions; one provider failure must not suppress actions from unrelated providers.")]
    private static async ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<RegisteredCodeFix>>> RegisterCodeFixesAsync(
        CodeFixProvider provider,
        string providerId,
        Document document,
        TextSpan requestedSpan,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var registeredActions = new List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)>();
        var context = new CodeFixContext(
            document,
            requestedSpan,
            diagnostics,
            (action, actionDiagnostics) => registeredActions.Add((action, actionDiagnostics)),
            cancellationToken);

        try
        {
            await provider.RegisterCodeFixesAsync(context);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ProviderFailure<IReadOnlyList<RegisteredCodeFix>>(
                providerId,
                "registering code fixes",
                exception);
        }

        var registeredCodeFixes = new List<RegisteredCodeFix>(registeredActions.Count);
        for (var rootIndex = 0; rootIndex < registeredActions.Count; rootIndex++)
        {
            var (action, actionDiagnostics) = registeredActions[rootIndex];
            registeredCodeFixes.Add(new RegisteredCodeFix
            {
                Action = action,
                Diagnostics = actionDiagnostics,
                TargetSpan = requestedSpan,
                RootIndex = rootIndex,
            });
        }

        return CodeActionProviderInvocationResult.Success<IReadOnlyList<RegisteredCodeFix>>(registeredCodeFixes);
    }

    private static bool IsFixableDiagnostic(ImmutableArray<string> fixableDiagnosticIds, string diagnosticId)
    {
        foreach (var fixableDiagnosticId in fixableDiagnosticIds)
        {
            if (string.Equals(fixableDiagnosticId, diagnosticId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> GetDistinctDiagnosticIds(ImmutableArray<Diagnostic> diagnostics)
    {
        var diagnosticIds = new List<string>();
        var seenDiagnosticIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var diagnostic in diagnostics)
        {
            if (seenDiagnosticIds.Add(diagnostic.Id))
            {
                diagnosticIds.Add(diagnostic.Id);
            }
        }

        return diagnosticIds;
    }

    private static List<CodeActionDiagnosticIdentity> GetDiagnosticIdentities(ImmutableArray<Diagnostic> diagnostics)
    {
        var identities = new List<CodeActionDiagnosticIdentity>();
        var seenIdentities = new HashSet<(string Id, string Message, int Start, int Length)>();
        foreach (var diagnostic in diagnostics)
        {
            var span = diagnostic.Location.SourceSpan;
            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            if (!seenIdentities.Add((diagnostic.Id, message, span.Start, span.Length)))
            {
                continue;
            }

            identities.Add(new CodeActionDiagnosticIdentity
            {
                Id = diagnostic.Id,
                Message = message,
                Start = span.Start,
                Length = span.Length,
            });
        }

        return identities;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Code Action providers are trusted external extensions; one provider failure must not suppress actions from unrelated providers.")]
    private static CodeActionProviderInvocationResult<IReadOnlyList<CodeActionFixAllScope>> GetFixAllScopes(
        CodeFixProvider provider,
        string providerId,
        CancellationToken cancellationToken)
    {
        FixAllProvider? fixAllProvider;
        FixAllScope[] supportedScopes = [];
        try
        {
            fixAllProvider = provider.GetFixAllProvider();
            if (fixAllProvider is not null)
            {
                supportedScopes = fixAllProvider.GetSupportedFixAllScopes().ToArray();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ProviderFailure<IReadOnlyList<CodeActionFixAllScope>>(
                providerId,
                "reading Fix-All capabilities",
                exception);
        }

        if (fixAllProvider is null)
        {
            return CodeActionProviderInvocationResult.Success<IReadOnlyList<CodeActionFixAllScope>>([]);
        }

        var scopes = new List<CodeActionFixAllScope>();
        foreach (var scope in supportedScopes)
        {
            var projectedScope = scope switch
            {
                FixAllScope.Document => CodeActionFixAllScope.Document,
                FixAllScope.Project => CodeActionFixAllScope.Project,
                FixAllScope.Solution => CodeActionFixAllScope.Solution,
                _ => (CodeActionFixAllScope?)null,
            };

            if (projectedScope is not null && !scopes.Contains(projectedScope.Value))
            {
                scopes.Add(projectedScope.Value);
            }
        }

        return CodeActionProviderInvocationResult.Success<IReadOnlyList<CodeActionFixAllScope>>(scopes);
    }

    private static CodeActionProviderInvocationResult<T> ProviderFailure<T>(
        string providerId,
        string operation,
        Exception exception)
        where T : class
    {
        var failure = new CodeActionProviderFailure
        {
            ProviderId = providerId,
            Operation = operation,
            ExceptionType = exception.GetType().Name,
        };

        return CodeActionProviderInvocationResult.Failed<T>(failure);
    }
}
