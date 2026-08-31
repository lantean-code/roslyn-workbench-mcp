namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Looks up policy-approved Code Action providers by stable identifier.
/// </summary>
internal interface ICodeActionProviderCatalog
{
    /// <summary>
    /// Gets matching refactoring providers.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The matching refactoring providers.</returns>
    IReadOnlyList<CodeRefactoringProvider> GetMatchingRefactoringProviders(string? providerId);

    /// <summary>
    /// Gets matching code fix providers.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The matching code fix providers.</returns>
    IReadOnlyList<CodeFixProvider> GetMatchingCodeFixProviders(string? providerId);

    /// <summary>
    /// Finds one refactoring provider by exact identifier.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The code refactoring provider.</returns>
    CodeRefactoringProvider? FindRefactoringProvider(string providerId);

    /// <summary>
    /// Finds one Code Fix provider by exact identifier.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The code fix provider.</returns>
    CodeFixProvider? FindCodeFixProvider(string providerId);
}
