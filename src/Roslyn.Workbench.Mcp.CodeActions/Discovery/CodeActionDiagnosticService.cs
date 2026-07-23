using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionDiagnosticService : ICodeActionDiagnosticService
{
    private readonly ICodeActionAnalyzerActivator _analyzerActivator;

    public CodeActionDiagnosticService(ICodeActionAnalyzerActivator analyzerActivator)
    {
        _analyzerActivator = analyzerActivator;
    }

    public async Task<IReadOnlyList<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var diagnostics = await GetDocumentDiagnosticsAsync(document, diagnosticIds, cancellationToken);
        var matchingDiagnostics = new List<Diagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Location.SourceSpan.IntersectsWith(span))
            {
                matchingDiagnostics.Add(diagnostic);
            }
        }

        return matchingDiagnostics;
    }

    public async Task<IReadOnlyList<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = await GetCompilationDiagnosticsAsync(
            document.Project,
            compilation,
            diagnosticIds,
            cancellationToken);

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);

        var documentDiagnostics = new List<Diagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Location.IsInSource && diagnostic.Location.SourceTree == syntaxTree)
            {
                documentDiagnostics.Add(diagnostic);
            }
        }

        return documentDiagnostics;
    }

    public async Task<IReadOnlyList<Diagnostic>> GetScopedCodeFixDiagnosticsAsync(
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var diagnostics = await GetDocumentDiagnosticsAsync(document, diagnosticIds, cancellationToken);
        if (diagnostics.Count > 0)
        {
            return diagnostics;
        }

        diagnostics = await GetAdditionalAnalyzerDiagnosticsAsync(document, span: null, diagnosticIds, analyzerTypeName, cancellationToken);
        if (diagnostics.Count > 0 || string.IsNullOrWhiteSpace(syntheticDiagnosticId))
        {
            return diagnostics;
        }

        var sourceText = await document.GetTextAsync(cancellationToken);
        var syntheticDiagnostic = await CreateSyntheticDiagnosticAsync(document, new TextSpan(0, sourceText.Length), syntheticDiagnosticId, cancellationToken);
        return syntheticDiagnostic is null ? [] : [syntheticDiagnostic];
    }

    public async Task<IReadOnlyList<Diagnostic>> GetLocationScopedCodeFixDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var diagnostics = await GetDocumentDiagnosticsAsync(document, span, diagnosticIds, cancellationToken);
        if (diagnostics.Count > 0)
        {
            return diagnostics;
        }

        diagnostics = await GetAdditionalAnalyzerDiagnosticsAsync(document, span, diagnosticIds, analyzerTypeName, cancellationToken);
        if (diagnostics.Count > 0 || string.IsNullOrWhiteSpace(syntheticDiagnosticId))
        {
            return diagnostics;
        }

        var syntheticDiagnostic = await CreateSyntheticDiagnosticAsync(document, span, syntheticDiagnosticId, cancellationToken);
        return syntheticDiagnostic is null ? [] : [syntheticDiagnostic];
    }

    public async Task<IReadOnlyList<Diagnostic>> GetProjectDiagnosticsAsync(
        Project project,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = await GetCompilationDiagnosticsAsync(project, compilation, diagnosticIds, cancellationToken);
        var projectDiagnostics = new List<Diagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (!diagnostic.Location.IsInSource)
            {
                projectDiagnostics.Add(diagnostic);
            }
        }

        return projectDiagnostics;
    }

    private static async Task<List<Diagnostic>> GetCompilationDiagnosticsAsync(
        Project project,
        Compilation compilation,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        HashSet<string>? diagnosticIdSet = null;
        if (diagnosticIds is { Count: > 0 })
        {
            diagnosticIdSet = new HashSet<string>(diagnosticIds, StringComparer.Ordinal);
        }

        var diagnostics = new List<Diagnostic>();
        AddMatchingDiagnostics(diagnostics, compilation.GetDiagnostics(cancellationToken), diagnosticIdSet);

        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        foreach (var analyzerReference in project.AnalyzerReferences)
        {
            foreach (var analyzer in analyzerReference.GetAnalyzers(project.Language))
            {
                if (diagnosticIdSet is null || SupportsAnyDiagnostic(analyzer, diagnosticIdSet))
                {
                    analyzers.Add(analyzer);
                }
            }
        }

        if (analyzers.Count == 0)
        {
            return diagnostics;
        }

        var analyzerDiagnostics = await compilation
            .WithAnalyzers(analyzers.ToImmutable(), project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

        AddMatchingDiagnostics(diagnostics, analyzerDiagnostics, diagnosticIdSet);
        return diagnostics;
    }

    private static void AddMatchingDiagnostics(
        List<Diagnostic> destination,
        ImmutableArray<Diagnostic> diagnostics,
        HashSet<string>? diagnosticIds)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnosticIds is null || diagnosticIds.Contains(diagnostic.Id))
            {
                destination.Add(diagnostic);
            }
        }
    }

    private static bool SupportsAnyDiagnostic(DiagnosticAnalyzer analyzer, HashSet<string> diagnosticIds)
    {
        foreach (var descriptor in analyzer.SupportedDiagnostics)
        {
            if (diagnosticIds.Contains(descriptor.Id))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<List<Diagnostic>> GetAdditionalAnalyzerDiagnosticsAsync(
        Document document,
        TextSpan? span,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(analyzerTypeName))
        {
            return [];
        }

        var activation = _analyzerActivator.Activate(analyzerTypeName);
        if (!activation.IsAvailable)
        {
            return [];
        }

        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            return [];
        }

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        if (syntaxTree is null)
        {
            return [];
        }

        var diagnostics = await compilation
            .WithAnalyzers([activation.Analyzer], document.Project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

        var matchingDiagnostics = new List<Diagnostic>();
        foreach (var diagnostic in diagnostics)
        {
            if (!diagnostic.Location.IsInSource || diagnostic.Location.SourceTree != syntaxTree)
            {
                continue;
            }

            if (diagnosticIds.Count > 0 && !diagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            {
                continue;
            }

            if (span is not null && !diagnostic.Location.SourceSpan.IntersectsWith(span.Value))
            {
                continue;
            }

            matchingDiagnostics.Add(diagnostic);
        }

        return matchingDiagnostics;
    }

    private static async Task<Diagnostic?> CreateSyntheticDiagnosticAsync(
        Document document,
        TextSpan span,
        string diagnosticId,
        CancellationToken cancellationToken)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        if (syntaxTree is null)
        {
            return null;
        }

        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            diagnosticId,
            diagnosticId,
            "Style",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden,
            isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, Location.Create(syntaxTree, span));
    }
}
