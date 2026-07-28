namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionDiagnosticService
{
    Task<CodeActionDiagnosticCollection> CollectDocumentDiagnosticsAsync(
        Document document,
        TextSpan? span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Diagnostic>> GetScopedCodeFixDiagnosticsAsync(
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Diagnostic>> GetLocationScopedCodeFixDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Diagnostic>> GetProjectDiagnosticsAsync(
        Project project,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);
}
