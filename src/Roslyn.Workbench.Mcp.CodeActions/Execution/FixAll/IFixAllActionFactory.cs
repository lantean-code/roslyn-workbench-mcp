namespace Roslyn.Workbench.Mcp.CodeActions.Execution.FixAll;

internal interface IFixAllActionFactory
{
    Task<FixAllActionCreationResult> CreateDocumentAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken);

    Task<FixAllActionCreationResult> CreateProjectAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Project project,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken);

    Task<FixAllActionCreationResult> CreateSolutionAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document originDocument,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken);
}
