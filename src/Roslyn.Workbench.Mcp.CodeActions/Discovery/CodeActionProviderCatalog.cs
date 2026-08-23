namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionProviderCatalog : ICodeActionProviderCatalog
{
    private readonly ICodeActionProviderSelection _providerSelection;
    private readonly ICodeActionPolicy _policy;

    public CodeActionProviderCatalog(
        ICodeActionProviderSelection providerSelection,
        ICodeActionPolicy policy)
    {
        _providerSelection = providerSelection;
        _policy = policy;
    }

    public IReadOnlyList<CodeRefactoringProvider> GetMatchingRefactoringProviders(string? providerId)
    {
        return GetMatchingProviders(_providerSelection.RefactoringProviders, providerId);
    }

    public IReadOnlyList<CodeFixProvider> GetMatchingCodeFixProviders(string? providerId)
    {
        return GetMatchingProviders(_providerSelection.CodeFixProviders, providerId);
    }

    public CodeRefactoringProvider? FindRefactoringProvider(string providerId)
    {
        return _providerSelection.RefactoringProviders.GetValueOrDefault(providerId);
    }

    public CodeFixProvider? FindCodeFixProvider(string providerId)
    {
        return _providerSelection.CodeFixProviders.GetValueOrDefault(providerId);
    }

    private List<TProvider> GetMatchingProviders<TProvider>(
        IReadOnlyDictionary<string, TProvider> providers,
        string? providerId)
        where TProvider : class
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            if (providers.TryGetValue(providerId, out var provider)
                && IsDiscoverableProvider(providerId))
            {
                return [provider];
            }

            return [];
        }

        var matchingProviders = new List<TProvider>();
        foreach (var (candidateProviderId, provider) in providers)
        {
            if (IsDiscoverableProvider(candidateProviderId))
            {
                matchingProviders.Add(provider);
            }
        }

        return matchingProviders;
    }

    private bool IsDiscoverableProvider(string providerId)
    {
        return _policy.EvaluateProvider(providerId).IsAllowed;
    }
}
