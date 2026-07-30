namespace Roslyn.Workbench.Mcp.CodeActions.Execution.FixAll;

internal sealed class WorkspaceFixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
{
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly IReadOnlyList<string> _diagnosticIds;

    public WorkspaceFixAllDiagnosticProvider(
        ICodeActionDiagnosticService diagnosticService,
        IReadOnlyList<string> diagnosticIds)
    {
        _diagnosticService = diagnosticService;
        _diagnosticIds = diagnosticIds;
    }

    public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
    {
        var diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(
            document,
            _diagnosticIds,
            cancellationToken);

        return diagnostics;
    }

    public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        var diagnostics = await _diagnosticService.GetProjectDiagnosticsAsync(
            project,
            _diagnosticIds,
            cancellationToken);

        return diagnostics;
    }

    public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        var documentDiagnostics = new List<Diagnostic>();
        foreach (var document in project.Documents)
        {
            var diagnostics = await _diagnosticService.GetDocumentDiagnosticsAsync(
                document,
                _diagnosticIds,
                cancellationToken);

            documentDiagnostics.AddRange(diagnostics);
        }

        var projectDiagnostics = await _diagnosticService.GetProjectDiagnosticsAsync(
            project,
            _diagnosticIds,
            cancellationToken);

        documentDiagnostics.AddRange(projectDiagnostics);
        return documentDiagnostics;
    }
}
