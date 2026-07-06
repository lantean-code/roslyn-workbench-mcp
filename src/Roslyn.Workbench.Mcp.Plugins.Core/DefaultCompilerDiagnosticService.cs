using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class DefaultCompilerDiagnosticService : ICompilerDiagnosticService
{
    public async ValueTask<IReadOnlyList<Diagnostic>> GetCompilerDiagnosticsAsync(
        IReadOnlyList<Document> selectedDocuments,
        CancellationToken cancellationToken)
    {
        var selectedDocumentIds = selectedDocuments.Select(static document => document.Id).ToImmutableHashSet();
        var diagnostics = new List<Diagnostic>();

        foreach (var project in selectedDocuments.Select(static document => document.Project).DistinctBy(static project => project.Id))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            diagnostics.AddRange(compilation
                .GetDiagnostics(cancellationToken)
                .Where(diagnostic => IsInSelectedDocument(diagnostic, project.Solution, selectedDocumentIds)));
        }

        return diagnostics
            .Distinct(DiagnosticComparer.Instance)
            .ToArray();
    }

    private static bool IsInSelectedDocument(Diagnostic diagnostic, Solution solution, ImmutableHashSet<DocumentId> selectedDocumentIds)
    {
        if (!diagnostic.Location.IsInSource || diagnostic.Location.SourceTree is null)
        {
            return false;
        }

        var document = solution.GetDocument(diagnostic.Location.SourceTree);
        return document is not null && selectedDocumentIds.Contains(document.Id);
    }

    private sealed class DiagnosticComparer : IEqualityComparer<Diagnostic>
    {
        public static readonly DiagnosticComparer Instance = new();

        public bool Equals(Diagnostic? x, Diagnostic? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return string.Equals(x.Id, y.Id, StringComparison.Ordinal)
                && string.Equals(x.GetMessage(), y.GetMessage(), StringComparison.Ordinal)
                && x.Severity == y.Severity
                && x.Location.SourceSpan.Equals(y.Location.SourceSpan)
                && string.Equals(x.Location.SourceTree?.FilePath, y.Location.SourceTree?.FilePath, StringComparison.Ordinal);
        }

        public int GetHashCode(Diagnostic obj)
        {
            return HashCode.Combine(
                obj.Id,
                obj.GetMessage(),
                obj.Severity,
                obj.Location.SourceSpan,
                obj.Location.SourceTree?.FilePath);
        }
    }
}
