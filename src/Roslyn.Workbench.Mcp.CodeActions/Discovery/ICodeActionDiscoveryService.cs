namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Invokes Code Action providers and flattens their registered actions into replayable leaves.
/// </summary>
internal interface ICodeActionDiscoveryService
{
    /// <summary>
    /// Reads a Code Fix provider's supported diagnostic identifiers.
    /// </summary>
    /// <param name="provider">The provider used to perform the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The code action provider invocation result.</returns>
    CodeActionProviderInvocationResult<CodeFixProviderMetadata> ReadCodeFixProviderMetadata(
        CodeFixProvider provider,
        CancellationToken cancellationToken);

    /// <summary>
    /// Discovers eligible refactoring leaves for a source span.
    /// </summary>
    /// <param name="provider">The provider used to perform the operation.</param>
    /// <param name="document">The document to inspect or modify.</param>
    /// <param name="span">The source span to which the operation applies.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the code action provider invocation result.</returns>
    ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> DiscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken);

    /// <summary>
    /// Discovers eligible Code Fix leaves for supplied diagnostics.
    /// </summary>
    /// <param name="providerMetadata">The metadata describing the Code Action provider.</param>
    /// <param name="document">The document to inspect or modify.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the code action provider invocation result.</returns>
    ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> DiscoverCodeFixesAsync(
        CodeFixProviderMetadata providerMetadata,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rediscovers refactoring actions for the selected source.
    /// </summary>
    /// <param name="provider">The provider used to perform the operation.</param>
    /// <param name="document">The document to inspect or modify.</param>
    /// <param name="span">The source span to which the operation applies.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the code action provider invocation result.</returns>
    ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> RediscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rediscovers code fixes for the selected diagnostic.
    /// </summary>
    /// <param name="providerMetadata">The metadata describing the Code Action provider.</param>
    /// <param name="document">The document to inspect or modify.</param>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the code action provider invocation result.</returns>
    ValueTask<CodeActionProviderInvocationResult<IReadOnlyList<DiscoveredCodeAction>>> RediscoverCodeFixesAsync(
        CodeFixProviderMetadata providerMetadata,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken);
}
