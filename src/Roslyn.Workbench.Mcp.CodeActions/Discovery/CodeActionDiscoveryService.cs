using System.Collections.Immutable;
using System.Globalization;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionDiscoveryService : ICodeActionDiscoveryService
{
    private readonly ICodeActionProviderSelection _providerSelection;
    private readonly ICodeActionDescriptorRegistry _descriptorRegistry;
    private readonly ICodeActionPolicy _policy;

    public CodeActionDiscoveryService(
        ICodeActionProviderSelection providerSelection,
        ICodeActionDescriptorRegistry descriptorRegistry,
        ICodeActionPolicy policy)
    {
        _providerSelection = providerSelection;
        _descriptorRegistry = descriptorRegistry;
        _policy = policy;
    }

    public IReadOnlyList<CodeRefactoringProvider> GetMatchingRefactoringProviders(string? providerId)
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            if (_providerSelection.RefactoringProviders.TryGetValue(providerId, out var provider)
                && IsDiscoverableProvider(providerId))
            {
                return [provider];
            }

            return [];
        }

        var matchingProviders = new List<CodeRefactoringProvider>();
        foreach (var (candidateProviderId, provider) in _providerSelection.RefactoringProviders)
        {
            if (IsDiscoverableProvider(candidateProviderId))
            {
                matchingProviders.Add(provider);
            }
        }

        return matchingProviders;
    }

    public IReadOnlyList<CodeFixProvider> GetMatchingCodeFixProviders(string? providerId)
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            if (_providerSelection.CodeFixProviders.TryGetValue(providerId, out var provider)
                && IsDiscoverableProvider(providerId))
            {
                return [provider];
            }

            return [];
        }

        var matchingProviders = new List<CodeFixProvider>();
        foreach (var (candidateProviderId, provider) in _providerSelection.CodeFixProviders)
        {
            if (IsDiscoverableProvider(candidateProviderId))
            {
                matchingProviders.Add(provider);
            }
        }

        return matchingProviders;
    }

    public CodeRefactoringProvider? FindRefactoringProvider(string providerId)
    {
        return _providerSelection.RefactoringProviders.GetValueOrDefault(providerId);
    }

    public CodeFixProvider? FindCodeFixProvider(string providerId)
    {
        return _providerSelection.CodeFixProviders.GetValueOrDefault(providerId);
    }

    public string GetProviderId(CodeFixProvider provider)
    {
        return CodeActionProviderIdentity.GetId(provider);
    }

    public string GetProviderId(CodeRefactoringProvider provider)
    {
        return CodeActionProviderIdentity.GetId(provider);
    }

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverRefactoringsAsync(
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

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        return await DiscoverCodeFixesCoreAsync(
            provider,
            document,
            diagnostics,
            enforcePolicy: true,
            cancellationToken);
    }

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> RediscoverRefactoringsAsync(
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

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> RediscoverCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        return await DiscoverCodeFixesCoreAsync(
            provider,
            document,
            diagnostics,
            enforcePolicy: false,
            cancellationToken);
    }

    private async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverRefactoringsCoreAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        bool enforcePolicy,
        CancellationToken cancellationToken)
    {
        var providerId = GetProviderId(provider);
        if (enforcePolicy && !_policy.EvaluateProvider(providerId).IsAllowed)
        {
            return [];
        }

        var capability = _descriptorRegistry.GetProviderCapability(providerId);
        var rootActions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, span, action => rootActions.Add(action), cancellationToken);
        await provider.ComputeRefactoringsAsync(context);

        return Flatten(
            rootActions,
            providerId,
            capability,
            DiscoveredActionKind.Refactoring,
            span,
            diagnosticIds: [],
            diagnostics: [],
            fixAllScopes: [],
            enforcePolicy);
    }

    private async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverCodeFixesCoreAsync(
        CodeFixProvider provider,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        bool enforcePolicy,
        CancellationToken cancellationToken)
    {
        var providerId = GetProviderId(provider);
        if (enforcePolicy && !_policy.EvaluateProvider(providerId).IsAllowed)
        {
            return [];
        }

        var capability = _descriptorRegistry.GetProviderCapability(providerId);
        var diagnosticsBySpan = new Dictionary<TextSpan, List<Diagnostic>>();
        var orderedSpans = new List<TextSpan>();
        var fixableDiagnosticIds = provider.FixableDiagnosticIds;
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
            return [];
        }

        var registeredActions = new List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics, TextSpan TargetSpan)>();
        foreach (var diagnosticSpan in orderedSpans)
        {
            var groupedDiagnostics = diagnosticsBySpan[diagnosticSpan].ToImmutableArray();

            await RegisterCodeFixesAsync(
                provider,
                document,
                diagnosticSpan,
                groupedDiagnostics,
                registeredActions,
                cancellationToken);
        }

        var discoveredActions = new List<DiscoveredCodeAction>();
        var fixAllScopes = GetFixAllScopes(provider);
        foreach (var (action, actionDiagnostics, targetSpan) in registeredActions)
        {
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
                capability,
                DiscoveredActionKind.CodeFix,
                targetSpan,
                diagnosticIds,
                diagnosticIdentities,
                actionFixAllScopes,
                [0],
                discoveredActions,
                enforcePolicy);
        }

        return discoveredActions;
    }

    private List<DiscoveredCodeAction> Flatten(
        List<CodeAction> rootActions,
        string providerId,
        CodeActionProviderCapability capability,
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
                capability,
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
        CodeActionProviderCapability capability,
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
                    capability,
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

        var descriptor = capability.Descriptor;
        if (capability.RequiresActionResolution)
        {
            descriptor = _descriptorRegistry.ResolveActionDependentDescriptor(action, providerId, action.Title);
        }

        discovered.Add(new DiscoveredCodeAction
        {
            Action = action,
            Kind = kind,
            ProviderId = providerId,
            Title = action.Title,
            Descriptor = descriptor,
            TargetSpan = targetSpan,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = path.ToArray(),
            DiagnosticIds = diagnosticIds,
            Diagnostics = diagnostics,
            FixAllScopes = fixAllScopes,
        });
    }

    private bool IsDiscoverableProvider(string providerId)
    {
        return _policy.EvaluateProvider(providerId).IsAllowed;
    }

    private static async Task RegisterCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        TextSpan requestedSpan,
        ImmutableArray<Diagnostic> diagnostics,
        List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics, TextSpan TargetSpan)> discovered,
        CancellationToken cancellationToken)
    {
        var context = new CodeFixContext(
            document,
            requestedSpan,
            diagnostics,
            (action, actionDiagnostics) => discovered.Add((action, actionDiagnostics, requestedSpan)),
            cancellationToken);

        await provider.RegisterCodeFixesAsync(context);
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

    private static List<CodeActionFixAllScope> GetFixAllScopes(CodeFixProvider provider)
    {
        var fixAllProvider = provider.GetFixAllProvider();
        if (fixAllProvider is null)
        {
            return [];
        }

        var scopes = new List<CodeActionFixAllScope>();
        foreach (var scope in fixAllProvider.GetSupportedFixAllScopes())
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

        return scopes;
    }
}
