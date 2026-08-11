namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionProjectDiagnosticCollection
{
    private readonly IReadOnlyDictionary<SyntaxTree, IReadOnlyList<Diagnostic>> _diagnosticsBySyntaxTree;

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public IReadOnlyList<Diagnostic> ProjectDiagnostics { get; }

    public IReadOnlyList<string> Warnings { get; }

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
