namespace Roslyn.Workbench.Mcp.CodeActions.Execution.FixAll;

internal sealed class FixAllActionFactory : IFixAllActionFactory
{
    private readonly ICodeActionDiagnosticService _diagnosticService;

    public FixAllActionFactory(ICodeActionDiagnosticService diagnosticService)
    {
        _diagnosticService = diagnosticService;
    }

    public Task<FixAllActionCreationResult> CreateDocumentAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        return CreateDocumentScopedAsync(
            provider,
            fixAllProvider,
            document,
            diagnosticIds,
            equivalenceKey,
            syntheticDiagnosticId,
            FixAllScope.Document,
            cancellationToken);
    }

    public Task<FixAllActionCreationResult> CreateProjectAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Project project,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            project,
            provider,
            FixAllScope.Project,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceFixAllDiagnosticProvider(_diagnosticService, diagnosticIds, syntheticDiagnosticId),
            cancellationToken);

        return CreateCoreAsync(fixAllProvider, fixAllContext);
    }

    public Task<FixAllActionCreationResult> CreateSolutionAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document originDocument,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        return CreateDocumentScopedAsync(
            provider,
            fixAllProvider,
            originDocument,
            diagnosticIds,
            equivalenceKey,
            syntheticDiagnosticId,
            FixAllScope.Solution,
            cancellationToken);
    }

    private Task<FixAllActionCreationResult> CreateDocumentScopedAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document originDocument,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        FixAllScope scope,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            originDocument,
            provider,
            scope,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceFixAllDiagnosticProvider(_diagnosticService, diagnosticIds, syntheticDiagnosticId),
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
