namespace Roslyn.Workbench.Mcp.CodeActions.Execution.FixAll;

/// <summary>
/// Builds Roslyn Fix All contexts and asks providers to create document, project, or solution actions.
/// </summary>
internal sealed class FixAllActionFactory : IFixAllActionFactory
{
    private readonly ICodeActionDiagnosticService _diagnosticService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixAllActionFactory"/> class.
    /// </summary>
    /// <param name="diagnosticService">The service used to obtain compiler diagnostics.</param>
    public FixAllActionFactory(ICodeActionDiagnosticService diagnosticService)
    {
        _diagnosticService = diagnosticService;
    }

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
    public Task<FixAllActionCreationResult> CreateDocumentAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        CancellationToken cancellationToken)
    {
        return CreateDocumentScopedAsync(
            provider,
            fixAllProvider,
            document,
            diagnosticIds,
            equivalenceKey,
            FixAllScope.Document,
            cancellationToken);
    }

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
    public Task<FixAllActionCreationResult> CreateProjectAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Project project,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            project,
            provider,
            FixAllScope.Project,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceFixAllDiagnosticProvider(_diagnosticService, diagnosticIds),
            cancellationToken);

        return CreateCoreAsync(fixAllProvider, fixAllContext);
    }

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
    public Task<FixAllActionCreationResult> CreateSolutionAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document originDocument,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        CancellationToken cancellationToken)
    {
        return CreateDocumentScopedAsync(
            provider,
            fixAllProvider,
            originDocument,
            diagnosticIds,
            equivalenceKey,
            FixAllScope.Solution,
            cancellationToken);
    }

    private Task<FixAllActionCreationResult> CreateDocumentScopedAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document originDocument,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        FixAllScope scope,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            originDocument,
            provider,
            scope,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceFixAllDiagnosticProvider(_diagnosticService, diagnosticIds),
            cancellationToken);

        return CreateCoreAsync(fixAllProvider, fixAllContext);
    }

    private static async Task<FixAllActionCreationResult> CreateCoreAsync(
        FixAllProvider fixAllProvider,
        FixAllContext fixAllContext)
    {
        var action = await fixAllProvider.GetFixAsync(fixAllContext);
        if (action is not null)
        {
            return FixAllActionCreationResult.Created(action);
        }

        var failure = new FixAllActionCreationFailure
        {
            Message = "The selected code fix could not produce a fix-all action.",
        };

        return FixAllActionCreationResult.Failed(failure);
    }
}
