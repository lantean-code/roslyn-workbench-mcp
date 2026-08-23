namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionDiscoveryService
{
    CodeActionProviderInvocationResult<CodeFixProviderMetadata> ReadCodeFixProviderMetadata(
        CodeFixProvider provider,
        CancellationToken cancellationToken);

    ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> DiscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken);

    ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> DiscoverCodeFixesAsync(
        CodeFixProviderMetadata providerMetadata,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken);

    ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> RediscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken);

    ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> RediscoverCodeFixesAsync(
        CodeFixProviderMetadata providerMetadata,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken);
}
