using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Execution;

internal sealed class WorkspaceFixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
{
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly IReadOnlyList<string> _diagnosticIds;
    private readonly string? _syntheticDiagnosticId;

    public WorkspaceFixAllDiagnosticProvider(
        ICodeActionDiagnosticService diagnosticService,
        IReadOnlyList<string> diagnosticIds,
        string? syntheticDiagnosticId)
    {
        _diagnosticService = diagnosticService;
        _diagnosticIds = diagnosticIds;
        _syntheticDiagnosticId = syntheticDiagnosticId;
    }

    public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
    {
        return await _diagnosticService
            .GetScopedCodeFixDiagnosticsAsync(document, _diagnosticIds, analyzerTypeName: null, _syntheticDiagnosticId, cancellationToken)
            .ConfigureAwait(false);
    }

    public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        return await _diagnosticService
            .GetProjectDiagnosticsAsync(project, _diagnosticIds, cancellationToken)
            .ConfigureAwait(false);
    }

    public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        var documentDiagnostics = new List<Diagnostic>();
        foreach (var document in project.Documents)
        {
            documentDiagnostics.AddRange(await _diagnosticService
                .GetDocumentDiagnosticsAsync(document, _diagnosticIds, cancellationToken)
                .ConfigureAwait(false));
        }

        documentDiagnostics.AddRange(await _diagnosticService
            .GetProjectDiagnosticsAsync(project, _diagnosticIds, cancellationToken)
            .ConfigureAwait(false));

        return documentDiagnostics.ToImmutableArray();
    }
}
