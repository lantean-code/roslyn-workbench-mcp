using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

/// <summary>
/// Runs Roslyn analyzers and retains diagnostics reported for a selected set of documents.
/// </summary>
internal sealed class AnalyzerDiagnosticService : IAnalyzerDiagnosticService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyzerDiagnosticService"/> class.
    /// </summary>
    public AnalyzerDiagnosticService()
    {
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        IReadOnlyList<Document> selectedDocuments,
        IReadOnlyList<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellationToken)
    {
        if (selectedDocuments.Count == 0 || analyzers.Count == 0)
        {
            return [];
        }

        var selectedDocumentIds = selectedDocuments
            .Select(static document => document.Id)
            .ToImmutableHashSet();

        var analyzerArray = analyzers.ToImmutableArray();
        var diagnostics = new List<Diagnostic>();
        foreach (var project in selectedDocuments
            .Select(static document => document.Project)
            .DistinctBy(static project => project.Id))
        {
            var projectDiagnostics = await GetProjectDiagnosticsAsync(
                project,
                analyzerArray,
                cancellationToken);

            diagnostics.AddRange(projectDiagnostics.Where(diagnostic =>
                IsInSelectedDocument(diagnostic, project.Solution, selectedDocumentIds)));
        }

        return diagnostics
            .Distinct(AnalyzerDiagnosticComparer.Instance)
            .ToArray();
    }

    private static async ValueTask<ImmutableArray<Diagnostic>> GetProjectDiagnosticsAsync(
        Project project,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            return [];
        }

        var failures = new ConcurrentQueue<Exception>();
        var analysisOptions = new CompilationWithAnalyzersOptions(
            project.AnalyzerOptions,
            (exception, analyzer, _) => failures.Enqueue(CreateAnalyzerFailure(analyzer, exception)),
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);

        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers, analysisOptions);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        if (failures.TryDequeue(out var failure))
        {
            throw failure;
        }

        return diagnostics;
    }

    private static InvalidOperationException CreateAnalyzerFailure(
        DiagnosticAnalyzer analyzer,
        Exception exception)
    {
        var analyzerType = analyzer.GetType();
        var analyzerName = analyzerType.FullName ?? analyzerType.Name;
        return new InvalidOperationException(
            $"Analyzer '{analyzerName}' failed during diagnostic analysis.",
            exception);
    }

    private static bool IsInSelectedDocument(
        Diagnostic diagnostic,
        Solution solution,
        ImmutableHashSet<DocumentId> selectedDocumentIds)
    {
        if (!diagnostic.Location.IsInSource || diagnostic.Location.SourceTree is null)
        {
            return false;
        }

        var document = solution.GetDocument(diagnostic.Location.SourceTree);
        return document is not null && selectedDocumentIds.Contains(document.Id);
    }

    private sealed class AnalyzerDiagnosticComparer : IEqualityComparer<Diagnostic>
    {
        public static readonly AnalyzerDiagnosticComparer Instance = new();

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
                && string.Equals(
                    x.GetMessage(CultureInfo.InvariantCulture),
                    y.GetMessage(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal)
                && x.Severity == y.Severity
                && x.Location.SourceSpan.Equals(y.Location.SourceSpan)
                && string.Equals(
                    x.Location.SourceTree?.FilePath,
                    y.Location.SourceTree?.FilePath,
                    StringComparison.Ordinal);
        }

        public int GetHashCode(Diagnostic obj)
        {
            return HashCode.Combine(
                obj.Id,
                obj.GetMessage(CultureInfo.InvariantCulture),
                obj.Severity,
                obj.Location.SourceSpan,
                obj.Location.SourceTree?.FilePath);
        }
    }
}
