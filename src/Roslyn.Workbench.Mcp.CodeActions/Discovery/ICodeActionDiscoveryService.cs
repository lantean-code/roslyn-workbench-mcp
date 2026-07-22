namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionDiscoveryService
{
    IReadOnlyList<CodeRefactoringProvider> GetMatchingRefactoringProviders(string? providerId);

    IReadOnlyList<CodeFixProvider> GetMatchingCodeFixProviders(string? providerId);

    CodeFixProvider? FindCodeFixProvider(string providerId);

    string GetProviderId(object provider);

    ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<DiscoveredCodeAction>> DiscoverCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken);
}
