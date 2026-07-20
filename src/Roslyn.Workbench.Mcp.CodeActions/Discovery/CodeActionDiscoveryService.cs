using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionDiscoveryService : ICodeActionDiscoveryService
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDescriptorRegistry _descriptorRegistry;

    public CodeActionDiscoveryService(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDescriptorRegistry descriptorRegistry)
    {
        _providerCatalog = providerCatalog;
        _descriptorRegistry = descriptorRegistry;
    }

    public IReadOnlyList<CodeRefactoringProvider> GetMatchingRefactoringProviders(string? providerId)
    {
        var matchingProviders = new List<CodeRefactoringProvider>();
        foreach (var provider in _providerCatalog.RefactoringProviders)
        {
            if (IsMatchingDiscoverableProvider(provider, providerId))
            {
                matchingProviders.Add(provider);
            }
        }

        matchingProviders.Sort(CompareProviderIds);
        return matchingProviders;
    }

    public IReadOnlyList<CodeFixProvider> GetMatchingCodeFixProviders(string? providerId)
    {
        var matchingProviders = new List<CodeFixProvider>();
        foreach (var provider in _providerCatalog.CodeFixProviders)
        {
            if (IsMatchingDiscoverableProvider(provider, providerId))
            {
                matchingProviders.Add(provider);
            }
        }

        matchingProviders.Sort(CompareProviderIds);
        return matchingProviders;
    }

    public CodeFixProvider? FindCodeFixProvider(string providerId)
    {
        foreach (var provider in _providerCatalog.CodeFixProviders)
        {
            if (string.Equals(GetProviderId(provider), providerId, StringComparison.Ordinal))
            {
                return provider;
            }
        }

        return null;
    }

    public string GetProviderId(object provider)
    {
        return provider.GetType().ToString();
    }

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var providerId = GetProviderId(provider);
        var capability = _descriptorRegistry.GetProviderCapability(providerId);
        if (!capability.ShouldDiscover)
        {
            return [];
        }

        var rootActions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, span, action => rootActions.Add(action), cancellationToken);
        await provider.ComputeRefactoringsAsync(context);

        return Flatten(rootActions, providerId, capability, DiscoveredActionKind.Refactoring, []);
    }

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var providerId = GetProviderId(provider);
        var capability = _descriptorRegistry.GetProviderCapability(providerId);
        if (!capability.ShouldDiscover)
        {
            return [];
        }

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

        var registeredActions = new List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)>();
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
        foreach (var (action, actionDiagnostics) in registeredActions)
        {
            var diagnosticIds = GetDistinctDiagnosticIds(actionDiagnostics);
            FlattenCore(
                action,
                providerId,
                capability,
                DiscoveredActionKind.CodeFix,
                diagnosticIds,
                [0],
                discoveredActions);
        }

        return discoveredActions;
    }

    private List<DiscoveredCodeAction> Flatten(
        List<CodeAction> rootActions,
        string providerId,
        CodeActionProviderCapability capability,
        DiscoveredActionKind kind,
        IReadOnlyList<string> diagnosticIds)
    {
        var discovered = new List<DiscoveredCodeAction>();
        var path = new List<int>();

        for (var index = 0; index < rootActions.Count; index++)
        {
            path.Add(index);
            FlattenCore(rootActions[index], providerId, capability, kind, diagnosticIds, path, discovered);
            path.RemoveAt(path.Count - 1);
        }

        return discovered;
    }

    private void FlattenCore(
        CodeAction action,
        string providerId,
        CodeActionProviderCapability capability,
        DiscoveredActionKind kind,
        IReadOnlyList<string> diagnosticIds,
        List<int> path,
        ICollection<DiscoveredCodeAction> discovered)
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
                    diagnosticIds,
                    path,
                    discovered);

                path.RemoveAt(path.Count - 1);
            }

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
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = path.ToArray(),
            DiagnosticIds = diagnosticIds,
        });
    }

    private bool IsMatchingDiscoverableProvider(object provider, string? requestedProviderId)
    {
        var providerId = GetProviderId(provider);
        if (!string.IsNullOrWhiteSpace(requestedProviderId)
            && !string.Equals(providerId, requestedProviderId, StringComparison.Ordinal))
        {
            return false;
        }

        return _descriptorRegistry.GetProviderCapability(providerId).ShouldDiscover;
    }

    private static async Task RegisterCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        TextSpan requestedSpan,
        ImmutableArray<Diagnostic> diagnostics,
        List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)> discovered,
        CancellationToken cancellationToken)
    {
        var context = new CodeFixContext(
            document,
            requestedSpan,
            diagnostics,
            (action, actionDiagnostics) => discovered.Add((action, actionDiagnostics)),
            cancellationToken);

        await provider.RegisterCodeFixesAsync(context);
    }

    private static int CompareProviderIds(CodeRefactoringProvider left, CodeRefactoringProvider right)
    {
        return StringComparer.Ordinal.Compare(left.GetType().ToString(), right.GetType().ToString());
    }

    private static int CompareProviderIds(CodeFixProvider left, CodeFixProvider right)
    {
        return StringComparer.Ordinal.Compare(left.GetType().ToString(), right.GetType().ToString());
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

}
