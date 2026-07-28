namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionDiscoveryService
{
    IReadOnlyList<CodeRefactoringProvider> GetMatchingRefactoringProviders(string? providerId);

    IReadOnlyList<CodeFixProvider> GetMatchingCodeFixProviders(string? providerId);

    CodeRefactoringProvider? FindRefactoringProvider(string providerId);

    CodeFixProvider? FindCodeFixProvider(string providerId);

    string GetProviderId(CodeFixProvider provider);

    string GetProviderId(CodeRefactoringProvider provider);

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

    ValueTask<IReadOnlyList<DiscoveredCodeAction>> RediscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<DiscoveredCodeAction>> RediscoverCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken);
}
