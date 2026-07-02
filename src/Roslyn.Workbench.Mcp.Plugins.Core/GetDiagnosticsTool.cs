using System.Collections.Immutable;

using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class GetDiagnosticsTool : QueryToolHandler<GetDiagnosticsRequest, DiagnosticsData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "get-diagnostics",
        Title = "Get Diagnostics",
        Description = "Returns compiler and analyzer diagnostics for a selected scope.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new GetDiagnosticsTool());
    }

    protected override async ValueTask<PluginExecutionResult<DiagnosticsData>> ExecuteCoreAsync(GetDiagnosticsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var documents = ToolExecutionHelpers.ResolveDocuments<DiagnosticsData>(request.Scope, context);
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
            diagnostics.AddRange(await GetProjectDiagnosticsAsync(project, selectedDocumentIds, restrictToSelectedDocuments, cancellationToken).ConfigureAwait(false));
        }
        var projectedDiagnostics = diagnostics
            .Where(diagnostic => MatchesDiagnosticFilters(diagnostic, request))
            .Select(diagnostic => new DiagnosticInfo
            {
                Id = diagnostic.Id,
                Severity = InspectionProjectionFactory.MapSeverity(diagnostic.Severity),
                Message = diagnostic.GetMessage(),
                Location = diagnostic.Location.IsInSource ? context.Resolver.CreateResolvedLocation(diagnostic.Location) : null,
            })
            .OrderBy(static diagnostic => diagnostic.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Location?.Span?.Start ?? int.MaxValue)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();

        return ToolExecutionHelpers.CreateBoundedCollectionResult(
            context,
            projectedDiagnostics,
            ToolExecutionHelpers.GetMaxResults(context, request.Limit),
            static (items, hasMore) => new DiagnosticsData
            {
                Diagnostics = items,
                ReturnedCount = items.Count,
                HasMore = hasMore,
            });
    }

    private static async ValueTask<IReadOnlyList<Diagnostic>> GetProjectDiagnosticsAsync(Project project, ImmutableHashSet<DocumentId> selectedDocumentIds, bool restrictToSelectedDocuments, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = compilation.GetDiagnostics(cancellationToken).ToList();
        var analyzers = project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(project.Language))
            .ToImmutableArray();

        if (!analyzers.IsDefaultOrEmpty)
        {
            diagnostics.AddRange(await compilation
                .WithAnalyzers(analyzers, project.AnalyzerOptions)
                .GetAnalyzerDiagnosticsAsync(cancellationToken)
                .ConfigureAwait(false));
        }

        return diagnostics
            .Where(diagnostic => !restrictToSelectedDocuments || IsInSelectedDocument(diagnostic, project.Solution, selectedDocumentIds))
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

    private static bool MatchesDiagnosticFilters(Diagnostic diagnostic, GetDiagnosticsRequest request)
    {
        if (request.Ids is not null && request.Ids.Count > 0 && !request.Ids.Contains(diagnostic.Id, StringComparer.Ordinal))
        {
            return false;
        }

        if (request.Severities is not null && request.Severities.Count > 0)
        {
            var severity = diagnostic.Severity.ToString();
            if (!request.Severities.Contains(severity, StringComparer.OrdinalIgnoreCase))
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
