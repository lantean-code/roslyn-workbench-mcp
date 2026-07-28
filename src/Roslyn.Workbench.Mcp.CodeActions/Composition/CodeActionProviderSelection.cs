namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed class CodeActionProviderSelection : ICodeActionProviderSelection
{
    public IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; }

    public IReadOnlyList<CodeFixProvider> CodeFixProviders { get; }

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

    private static List<TProvider> SelectEligibleProviders<TProvider>(
        IReadOnlyList<TProvider> providers,
        ICodeActionPolicy policy,
        Func<TProvider, string> getProviderId)
        where TProvider : class
    {
        var eligibleProviders = new List<TProvider>(providers.Count);
        foreach (var provider in providers)
        {
            var providerId = getProviderId(provider);
            var decision = policy.EvaluateProvider(providerId);
            if (decision.IsAllowed)
            {
                eligibleProviders.Add(provider);
            }
        }

        return eligibleProviders;
    }
}
