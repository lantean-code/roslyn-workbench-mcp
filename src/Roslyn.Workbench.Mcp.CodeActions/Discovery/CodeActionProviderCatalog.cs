namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Looks up policy-approved Code Action providers by stable identifier.
/// </summary>
internal sealed class CodeActionProviderCatalog : ICodeActionProviderCatalog
{
    private readonly ICodeActionProviderSelection _providerSelection;
    private readonly ICodeActionPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionProviderCatalog"/> class.
    /// </summary>
    /// <param name="providerSelection">The composed providers that passed initial policy selection.</param>
    /// <param name="policy">The policy used to confirm providers remain discoverable.</param>
    public CodeActionProviderCatalog(
        ICodeActionProviderSelection providerSelection,
        ICodeActionPolicy policy)
    {
        _providerSelection = providerSelection;
        _policy = policy;
    }

    /// <summary>
    /// Gets matching refactoring providers.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The matching refactoring providers.</returns>
    public IReadOnlyList<CodeRefactoringProvider> GetMatchingRefactoringProviders(string? providerId)
    {
        return GetMatchingProviders(_providerSelection.RefactoringProviders, providerId);
    }

    /// <summary>
    /// Gets matching code fix providers.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The matching code fix providers.</returns>
    public IReadOnlyList<CodeFixProvider> GetMatchingCodeFixProviders(string? providerId)
    {
        return GetMatchingProviders(_providerSelection.CodeFixProviders, providerId);
    }

    /// <summary>
    /// Finds one refactoring provider by exact identifier.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The code refactoring provider.</returns>
    public CodeRefactoringProvider? FindRefactoringProvider(string providerId)
    {
        return _providerSelection.RefactoringProviders.GetValueOrDefault(providerId);
    }

    /// <summary>
    /// Finds one Code Fix provider by exact identifier.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The code fix provider.</returns>
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
