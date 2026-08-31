namespace Roslyn.Workbench.Mcp.CodeActions.Execution.FixAll;

/// <summary>
/// Creates provider-backed Fix All actions for supported scopes.
/// </summary>
internal interface IFixAllActionFactory
{
    /// <summary>
    /// Creates a Fix All action scoped to one document.
    /// </summary>
    /// <param name="provider">The originating Code Fix provider.</param>
    /// <param name="fixAllProvider">The provider used to create the Fix All action.</param>
    /// <param name="document">The document to fix.</param>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <param name="equivalenceKey">The equivalence key used to select matching Code Actions.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The provider-created Fix All action or a creation failure.</returns>
    Task<FixAllActionCreationResult> CreateDocumentAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a Fix All action scoped to one project.
    /// </summary>
    /// <param name="provider">The originating Code Fix provider.</param>
    /// <param name="fixAllProvider">The provider used to create the Fix All action.</param>
    /// <param name="project">The project to fix.</param>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <param name="equivalenceKey">The equivalence key used to select matching Code Actions.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The provider-created Fix All action or a creation failure.</returns>
    Task<FixAllActionCreationResult> CreateProjectAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Project project,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a Fix All action scoped to the complete solution.
    /// </summary>
    /// <param name="provider">The originating Code Fix provider.</param>
    /// <param name="fixAllProvider">The provider used to create the Fix All action.</param>
    /// <param name="originDocument">The document from which the Fix All action was requested.</param>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <param name="equivalenceKey">The equivalence key used to select matching Code Actions.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The provider-created Fix All action or a creation failure.</returns>
    Task<FixAllActionCreationResult> CreateSolutionAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document originDocument,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        CancellationToken cancellationToken);
}
