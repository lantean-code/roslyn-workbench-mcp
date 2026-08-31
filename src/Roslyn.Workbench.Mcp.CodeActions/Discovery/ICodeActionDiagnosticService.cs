namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Collects compiler and analyzer diagnostics used for Code Fix discovery.
/// </summary>
internal interface ICodeActionDiagnosticService
{
    /// <summary>
    /// Collects project and source diagnostics for a project.
    /// </summary>
    /// <param name="project">The project to inspect or modify.</param>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the Code Action project diagnostic collection.</returns>
    Task<CodeActionProjectDiagnosticCollection> CollectProjectDiagnosticsAsync(
        Project project,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Collects diagnostics belonging to a document and optional source span.
    /// </summary>
    /// <param name="document">The document to inspect or modify.</param>
    /// <param name="span">The source span to which the operation applies.</param>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the Code Action diagnostic collection.</returns>
    Task<CodeActionDiagnosticCollection> CollectDocumentDiagnosticsAsync(
        Document document,
        TextSpan? span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets diagnostics intersecting a source span in a document.
    /// </summary>
    /// <param name="document">The document to inspect or modify.</param>
    /// <param name="span">The source span to which the operation applies.</param>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the document diagnostics.</returns>
    Task<IReadOnlyList<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets all diagnostics belonging to a document.
    /// </summary>
    /// <param name="document">The document to inspect or modify.</param>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the document diagnostics.</returns>
    Task<IReadOnlyList<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets all diagnostics retained for a project.
    /// </summary>
    /// <param name="project">The project to inspect or modify.</param>
    /// <param name="diagnosticIds">The diagnostic identifiers that constrain the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the project diagnostics.</returns>
    Task<IReadOnlyList<Diagnostic>> GetProjectDiagnosticsAsync(
        Project project,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken);
}
