namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Organizes project analyzer diagnostics into project-level and per-document views.
/// </summary>
internal sealed class CodeActionProjectDiagnosticCollection
{
    private readonly IReadOnlyDictionary<SyntaxTree, IReadOnlyList<Diagnostic>> _diagnosticsBySyntaxTree;

    /// <summary>
    /// Gets all retained diagnostics belonging to the project.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets diagnostics that are not tied to a source document.
    /// </summary>
    public IReadOnlyList<Diagnostic> ProjectDiagnostics { get; }

    /// <summary>
    /// Gets non-fatal analyzer activation and execution warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionProjectDiagnosticCollection"/> class.
    /// </summary>
    /// <param name="diagnostics">The diagnostics to include in the operation result.</param>
    /// <param name="projectDiagnostics">The project-level diagnostics returned by analyzer execution.</param>
    /// <param name="diagnosticsBySyntaxTree">The source diagnostics grouped by their syntax tree.</param>
    /// <param name="warnings">The warnings to include in the operation result.</param>
    public CodeActionProjectDiagnosticCollection(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<Diagnostic> projectDiagnostics,
        IReadOnlyDictionary<SyntaxTree, IReadOnlyList<Diagnostic>> diagnosticsBySyntaxTree,
        IReadOnlyList<string> warnings)
    {
        Diagnostics = diagnostics;
        ProjectDiagnostics = projectDiagnostics;
        _diagnosticsBySyntaxTree = diagnosticsBySyntaxTree;
        Warnings = warnings;
    }

    /// <summary>
    /// Filters collected diagnostics to the project and indexes source diagnostics by syntax tree.
    /// </summary>
    /// <param name="project">The project to inspect or modify.</param>
    /// <param name="collection">The source collection copied into the immutable result.</param>
    /// <returns>The Code Action project diagnostic collection.</returns>
    public static CodeActionProjectDiagnosticCollection Create(
        Project project,
        CodeActionDiagnosticCollection collection)
    {
        var diagnostics = new List<Diagnostic>();
        var projectDiagnostics = new List<Diagnostic>();
        var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, List<Diagnostic>>();
        foreach (var diagnostic in collection.Diagnostics)
        {
            if (!diagnostic.Location.IsInSource)
            {
                diagnostics.Add(diagnostic);
                projectDiagnostics.Add(diagnostic);
                continue;
            }

            var syntaxTree = diagnostic.Location.SourceTree;
            if (syntaxTree is null || project.GetDocument(syntaxTree) is null)
            {
                continue;
            }

            diagnostics.Add(diagnostic);
            if (!diagnosticsBySyntaxTree.TryGetValue(syntaxTree, out var documentDiagnostics))
            {
                documentDiagnostics = [];
                diagnosticsBySyntaxTree.Add(syntaxTree, documentDiagnostics);
            }

            documentDiagnostics.Add(diagnostic);
        }

        var documentDiagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>();
        foreach (var (syntaxTree, documentDiagnostics) in diagnosticsBySyntaxTree)
        {
            documentDiagnosticsBySyntaxTree.Add(syntaxTree, documentDiagnostics);
        }

        return new CodeActionProjectDiagnosticCollection(
            diagnostics,
            projectDiagnostics,
            documentDiagnosticsBySyntaxTree,
            collection.Warnings);
    }

    /// <summary>
    /// Gets diagnostics for a syntax tree, optionally restricted to a source span.
    /// </summary>
    /// <param name="syntaxTree">The syntax tree whose source diagnostics are requested.</param>
    /// <param name="span">The source span to which the operation applies.</param>
    /// <returns>The document diagnostics.</returns>
    public IReadOnlyList<Diagnostic> GetDocumentDiagnostics(SyntaxTree syntaxTree, TextSpan? span)
    {
        if (!_diagnosticsBySyntaxTree.TryGetValue(syntaxTree, out var diagnostics))
        {
            return [];
        }

        if (span is null)
        {
            return diagnostics;
        }

        var intersectingDiagnostics = new List<Diagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Location.SourceSpan.IntersectsWith(span.Value))
            {
                intersectingDiagnostics.Add(diagnostic);
            }
        }

        return intersectingDiagnostics;
    }
}
