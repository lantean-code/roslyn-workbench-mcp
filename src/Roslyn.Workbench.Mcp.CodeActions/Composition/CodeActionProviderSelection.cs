using System.Collections.Frozen;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Indexes the composed Code Action providers that pass Host policy.
/// </summary>
internal sealed class CodeActionProviderSelection : ICodeActionProviderSelection
{
    /// <summary>
    /// Gets eligible refactoring providers keyed by stable provider identifier.
    /// </summary>
    public FrozenDictionary<string, CodeRefactoringProvider> RefactoringProviders { get; }

    /// <summary>
    /// Gets eligible Code Fix providers keyed by stable provider identifier.
    /// </summary>
    public FrozenDictionary<string, CodeFixProvider> CodeFixProviders { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionProviderSelection"/> class.
    /// </summary>
    /// <param name="composition">The composed provider set to filter.</param>
    /// <param name="policy">The policy that excludes unsupported providers.</param>
    public CodeActionProviderSelection(
        ICodeActionComposition composition,
        ICodeActionPolicy policy)
    {
        RefactoringProviders = SelectEligibleProviders(
            composition.RefactoringProviders,
            policy,
            CodeActionProviderIdentity.GetId);

        CodeFixProviders = SelectEligibleProviders(
            composition.CodeFixProviders,
            policy,
            CodeActionProviderIdentity.GetId);
    }

    private static FrozenDictionary<string, TProvider> SelectEligibleProviders<TProvider>(
        IReadOnlyList<TProvider> providers,
        ICodeActionPolicy policy,
        Func<TProvider, string> getProviderId)
        where TProvider : class
    {
        var eligibleProviders = new Dictionary<string, TProvider>(providers.Count, StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            var providerId = getProviderId(provider);
            var decision = policy.EvaluateProvider(providerId);
            if (!decision.IsAllowed)
            {
                continue;
            }

            if (!eligibleProviders.TryAdd(providerId, provider))
            {
                throw new InvalidOperationException(
                    $"Code Action composition contains multiple eligible providers with ID '{providerId}'.");
            }
        }

        return eligibleProviders.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
