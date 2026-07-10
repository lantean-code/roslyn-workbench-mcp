namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal interface ICodeActionOperationService
{
    ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> CreateMutationProposalAsync(
        CodeAction action,
        string summary,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);

    ValueTask<int> CountChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken);

    Task<CodeActionApplyResult> ApplyFixAllAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        TextSpan originSpan,
        FixAllScope scope,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken);

    Task<CodeActionApplyResult> ApplyFixAllAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Project project,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken);
}
