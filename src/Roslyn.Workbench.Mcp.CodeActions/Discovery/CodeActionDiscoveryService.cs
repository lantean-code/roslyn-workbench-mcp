using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionDiscoveryService : ICodeActionDiscoveryService
{
    private readonly ICodeActionRuntime _runtime;

    public CodeActionDiscoveryService(ICodeActionRuntime runtime)
    {
        _runtime = runtime;
    }

    public IReadOnlyList<CodeRefactoringProvider> GetMatchingRefactoringProviders(string? providerId)
    {
        return _runtime.RefactoringProviders
            .Where(provider => string.IsNullOrWhiteSpace(providerId) || string.Equals(GetProviderId(provider), providerId, StringComparison.Ordinal))
            .OrderBy(GetProviderId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<CodeFixProvider> GetMatchingCodeFixProviders(string? providerId)
    {
        return _runtime.CodeFixProviders
            .Where(provider => string.IsNullOrWhiteSpace(providerId) || string.Equals(GetProviderId(provider), providerId, StringComparison.Ordinal))
            .OrderBy(GetProviderId, StringComparer.Ordinal)
            .ToArray();
    }

    public CodeFixProvider? FindCodeFixProvider(string providerId)
    {
        return _runtime.CodeFixProviders.SingleOrDefault(candidate => string.Equals(GetProviderId(candidate), providerId, StringComparison.Ordinal));
    }

    public string GetProviderId(object provider)
    {
        return provider.GetType().FullName ?? provider.GetType().Name;
    }

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverProviderRefactoringsAsync(
        string providerId,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var provider = _runtime.RefactoringProviders.SingleOrDefault(candidate => string.Equals(GetProviderId(candidate), providerId, StringComparison.Ordinal));
        return provider is null
            ? []
            : await DiscoverRefactoringsAsync(provider, document, span, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverProviderCodeFixesAsync(
        string providerId,
        Document document,
        TextSpan span,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var provider = _runtime.CodeFixProviders.SingleOrDefault(candidate => string.Equals(GetProviderId(candidate), providerId, StringComparison.Ordinal));
        return provider is null
            ? []
            : await DiscoverCodeFixesAsync(provider, document, span, diagnostics, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var rootActions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, span, action => rootActions.Add(action), cancellationToken);
        await provider.ComputeRefactoringsAsync(context).ConfigureAwait(false);

        return Flatten(rootActions, GetProviderId(provider), DiscoveredActionKind.Refactoring, []);
    }

    public async ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        TextSpan span,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var matchingDiagnostics = diagnostics
            .Where(diagnostic => provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToImmutableArray();
        if (matchingDiagnostics.IsDefaultOrEmpty)
        {
            return [];
        }

        var discovered = new List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)>();
        try
        {
            await RegisterCodeFixesAsync(provider, document, span, matchingDiagnostics, discovered, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            discovered.Clear();
            foreach (var diagnostic in matchingDiagnostics)
            {
                await RegisterCodeFixesAsync(provider, document, diagnostic.Location.SourceSpan, [diagnostic], discovered, cancellationToken).ConfigureAwait(false);
            }
        }

        return discovered
            .SelectMany(entry => Flatten(
                [entry.Action],
                GetProviderId(provider),
                DiscoveredActionKind.CodeFix,
                entry.Diagnostics
                    .Select(static diagnostic => diagnostic.Id)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }

    private static async Task RegisterCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        TextSpan requestedSpan,
        ImmutableArray<Diagnostic> diagnostics,
        ICollection<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)> discovered,
        CancellationToken cancellationToken)
    {
        var contextSpan = ExpandCodeFixContextSpan(requestedSpan, diagnostics);
        var context = new CodeFixContext(
            document,
            contextSpan,
            diagnostics,
            (action, actionDiagnostics) => discovered.Add((action, actionDiagnostics)),
            cancellationToken);
        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
    }

    private static TextSpan ExpandCodeFixContextSpan(TextSpan requestedSpan, ImmutableArray<Diagnostic> diagnostics)
    {
        var start = requestedSpan.Start;
        var end = requestedSpan.End;

        foreach (var diagnostic in diagnostics)
        {
            if (!diagnostic.Location.IsInSource)
            {
                continue;
            }

            start = Math.Min(start, diagnostic.Location.SourceSpan.Start);
            end = Math.Max(end, diagnostic.Location.SourceSpan.End);
        }

        return TextSpan.FromBounds(start, end);
    }

    private static IReadOnlyList<DiscoveredCodeAction> Flatten(
        IReadOnlyList<CodeAction> rootActions,
        string providerId,
        DiscoveredActionKind kind,
        IReadOnlyList<string> diagnosticIds)
    {
        var discovered = new List<DiscoveredCodeAction>();

        for (var index = 0; index < rootActions.Count; index++)
        {
            FlattenCore(rootActions[index], providerId, kind, diagnosticIds, [index], discovered);
        }

        return discovered;
    }

    private static void FlattenCore(
        CodeAction action,
        string providerId,
        DiscoveredActionKind kind,
        IReadOnlyList<string> diagnosticIds,
        IReadOnlyList<int> path,
        ICollection<DiscoveredCodeAction> discovered)
    {
        var nested = action.NestedActions;
        if (!nested.IsDefaultOrEmpty)
        {
            for (var index = 0; index < nested.Length; index++)
            {
                FlattenCore(
                    nested[index],
                    providerId,
                    kind,
                    diagnosticIds,
                    path.Concat([index]).ToArray(),
                    discovered);
            }

            return;
        }

        discovered.Add(new DiscoveredCodeAction
        {
            Action = action,
            Kind = kind,
            ProviderId = providerId,
            Title = action.Title,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = path.ToArray(),
            DiagnosticIds = diagnosticIds.ToArray(),
        });
    }
}
