namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionProviderCatalog
{
    IReadOnlyList<CodeRefactoringProvider> GetMatchingRefactoringProviders(string? providerId);

    IReadOnlyList<CodeFixProvider> GetMatchingCodeFixProviders(string? providerId);

    CodeRefactoringProvider? FindRefactoringProvider(string providerId);

    CodeFixProvider? FindCodeFixProvider(string providerId);
}
