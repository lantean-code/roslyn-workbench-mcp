using System.Collections.Frozen;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed class CodeActionProviderSelection : ICodeActionProviderSelection
{
    public FrozenDictionary<string, CodeRefactoringProvider> RefactoringProviders { get; }

    public FrozenDictionary<string, CodeFixProvider> CodeFixProviders { get; }

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
