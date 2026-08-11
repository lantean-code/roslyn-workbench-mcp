using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool("get-diagnostics", "Get Diagnostics", "Returns compiler and analyzer diagnostics for a selected scope.")]
internal sealed class GetDiagnosticsTool : QueryToolHandler<GetDiagnosticsRequest, DiagnosticsData>
{
    protected override async ValueTask<PluginExecutionResult<DiagnosticsData>> ExecuteCoreAsync(GetDiagnosticsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<DiagnosticsData>(request.Scope, context);
        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var selectedDocuments = documents.Value;
        var selectedDocumentIds = selectedDocuments.Select(static document => document.Id).ToImmutableHashSet();
        var restrictToSelectedDocuments = request.Scope?.Kind == ScopeKind.Document;
        var diagnostics = new List<Diagnostic>();
        foreach (var project in selectedDocuments.Select(static document => document.Project).DistinctBy(static project => project.Id))
        {
            diagnostics.AddRange(await GetProjectDiagnosticsAsync(project, selectedDocumentIds, restrictToSelectedDocuments, cancellationToken));
        }

        var includedIds = request.Ids is { Count: > 0 }
            ? request.Ids.ToHashSet(StringComparer.Ordinal)
            : null;

        var includedSeverities = request.Severities is { Count: > 0 }
            ? request.Severities.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var maxResults = request.EffectiveDiagnosticsLimit;
        var projectedDiagnostics = new List<DiagnosticInfo>();
        var filteredDiagnostics = new List<Diagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (MatchesDiagnosticFilters(diagnostic, includedIds, includedSeverities))
            {
                filteredDiagnostics.Add(diagnostic);
            }
        }

        var orderedDiagnostics = filteredDiagnostics
            .OrderBy(static diagnostic => diagnostic.Location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal);

        foreach (var diagnostic in orderedDiagnostics)
        {
            if (projectedDiagnostics.Count == maxResults)
            {
                break;
            }

            projectedDiagnostics.Add(CompilerDiagnosticHelpers.CreateDiagnosticInfo(diagnostic, context));
        }

        var data = new DiagnosticsData
        {
            Diagnostics = BoundedCollection.CreatePrebounded(
                projectedDiagnostics,
                filteredDiagnostics.Count),
        };

        return PluginExecutionResult.Success(data);
    }

    private static async ValueTask<IReadOnlyList<Diagnostic>> GetProjectDiagnosticsAsync(Project project, ImmutableHashSet<DocumentId> selectedDocumentIds, bool restrictToSelectedDocuments, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            return [];
        }

        var analyzers = project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(project.Language))
            .ToImmutableArray();

        ImmutableArray<Diagnostic> diagnostics;
        if (analyzers.IsDefaultOrEmpty)
        {
            diagnostics = compilation.GetDiagnostics(cancellationToken);
        }
        else
        {
            var compilationWithAnalyzers = compilation.WithAnalyzers(
                analyzers,
                project.AnalyzerOptions);

            diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken);
        }

        var uniqueDiagnostics = new HashSet<Diagnostic>(DiagnosticComparer.Instance);
        var selectedDiagnostics = new List<Diagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (!restrictToSelectedDocuments
                || IsInSelectedDocument(diagnostic, project.Solution, selectedDocumentIds))
            {
                if (uniqueDiagnostics.Add(diagnostic))
                {
                    selectedDiagnostics.Add(diagnostic);
                }
            }
        }

        return selectedDiagnostics;
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

    private static bool MatchesDiagnosticFilters(Diagnostic diagnostic, HashSet<string>? includedIds, HashSet<string>? includedSeverities)
    {
        if (includedIds is not null && !includedIds.Contains(diagnostic.Id))
        {
            return false;
        }

        if (includedSeverities is not null)
        {
            var severity = diagnostic.Severity.ToString();
            if (!includedSeverities.Contains(severity))
            {
                return false;
            }
        }

        return true;
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
                && string.Equals(x.GetMessage(CultureInfo.InvariantCulture), y.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                && x.Severity == y.Severity
                && x.Location.SourceSpan.Equals(y.Location.SourceSpan)
                && string.Equals(x.Location.SourceTree?.FilePath, y.Location.SourceTree?.FilePath, StringComparison.Ordinal);
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
