namespace Roslyn.Workbench.Mcp.CodeActions.Execution.FixAll;

/// <summary>
/// Supplies cached project diagnostics to Roslyn while it computes a Fix All action.
/// </summary>
internal sealed class WorkspaceFixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
{
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly IReadOnlyList<string> _diagnosticIds;
    private readonly object _projectDiagnosticTasksLock = new();
    private readonly Dictionary<ProjectId, Task<CodeActionProjectDiagnosticCollection>> _projectDiagnosticTasks = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceFixAllDiagnosticProvider"/> class.
    /// </summary>
    /// <param name="diagnosticService">The service used to obtain compiler diagnostics.</param>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    public WorkspaceFixAllDiagnosticProvider(
        ICodeActionDiagnosticService diagnosticService,
        IReadOnlyList<string> diagnosticIds)
    {
        _diagnosticService = diagnosticService;
        _diagnosticIds = diagnosticIds;
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
    {
        var diagnosticTask = GetOrCreateProjectDiagnosticTask(document.Project, cancellationToken);
        var collection = await diagnosticTask.WaitAsync(cancellationToken);
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        if (syntaxTree is null)
        {
            return [];
        }

        return collection.GetDocumentDiagnostics(syntaxTree, span: null);
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        var diagnosticTask = GetOrCreateProjectDiagnosticTask(project, cancellationToken);
        var collection = await diagnosticTask.WaitAsync(cancellationToken);

        return collection.ProjectDiagnostics;
    }

    /// <inheritdoc/>
    public override async Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
    {
        var diagnosticTask = GetOrCreateProjectDiagnosticTask(project, cancellationToken);
        var collection = await diagnosticTask.WaitAsync(cancellationToken);

        return collection.Diagnostics;
    }

    private Task<CodeActionProjectDiagnosticCollection> GetOrCreateProjectDiagnosticTask(
        Project project,
        CancellationToken cancellationToken)
    {
        lock (_projectDiagnosticTasksLock)
        {
            if (_projectDiagnosticTasks.TryGetValue(project.Id, out var diagnosticTask))
            {
                return diagnosticTask;
            }

            diagnosticTask = _diagnosticService.CollectProjectDiagnosticsAsync(
                project,
                _diagnosticIds,
                cancellationToken);

            _projectDiagnosticTasks.Add(project.Id, diagnosticTask);
            return diagnosticTask;
        }
    }
}
