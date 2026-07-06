using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Discovery;

internal interface ICodeActionDiagnosticService
{
    Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);

    Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);

    Task<ImmutableArray<Diagnostic>> GetScopedCodeFixDiagnosticsAsync(
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken);

    Task<ImmutableArray<Diagnostic>> GetLocationScopedCodeFixDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken);

    Task<ImmutableArray<Diagnostic>> GetProjectDiagnosticsAsync(
        Project project,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);
}
