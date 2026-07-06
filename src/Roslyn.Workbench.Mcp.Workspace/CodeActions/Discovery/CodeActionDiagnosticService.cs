using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Discovery;

internal sealed class CodeActionDiagnosticService : ICodeActionDiagnosticService
{
    public async Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        return (await GetDocumentDiagnosticsAsync(document, diagnosticIds, cancellationToken).ConfigureAwait(false))
            .Where(diagnostic => diagnostic.Location.SourceSpan.IntersectsWith(span))
            .ToImmutableArray();
    }

    public async Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = compilation.GetDiagnostics(cancellationToken).ToList();
        var analyzers = document.Project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(document.Project.Language))
            .ToImmutableArray();
        if (!analyzers.IsDefaultOrEmpty)
        {
            diagnostics.AddRange(await compilation
                .WithAnalyzers(analyzers, document.Project.AnalyzerOptions)
                .GetAnalyzerDiagnosticsAsync(cancellationToken)
                .ConfigureAwait(false));
        }

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);

        return diagnostics
            .Where(diagnostic => diagnostic.Location.IsInSource && diagnostic.Location.SourceTree == syntaxTree)
            .Where(diagnostic => diagnosticIds is null || diagnosticIds.Count == 0 || diagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToImmutableArray();
    }

    public async Task<ImmutableArray<Diagnostic>> GetScopedCodeFixDiagnosticsAsync(
        Document document,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var diagnostics = await GetDocumentDiagnosticsAsync(document, diagnosticIds, cancellationToken).ConfigureAwait(false);
        if (!diagnostics.IsDefaultOrEmpty)
        {
            return diagnostics;
        }

        diagnostics = await GetAdditionalAnalyzerDiagnosticsAsync(document, span: null, diagnosticIds, analyzerTypeName, cancellationToken).ConfigureAwait(false);
        if (!diagnostics.IsDefaultOrEmpty || string.IsNullOrWhiteSpace(syntheticDiagnosticId))
        {
            return diagnostics;
        }

        var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var syntheticDiagnostic = await CreateSyntheticDiagnosticAsync(document, new TextSpan(0, sourceText.Length), syntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
        return syntheticDiagnostic is null ? [] : [syntheticDiagnostic];
    }

    public async Task<ImmutableArray<Diagnostic>> GetLocationScopedCodeFixDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string> diagnosticIds,
        string? analyzerTypeName,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var diagnostics = await GetDocumentDiagnosticsAsync(document, span, diagnosticIds, cancellationToken).ConfigureAwait(false);
        if (!diagnostics.IsDefaultOrEmpty)
        {
            return diagnostics;
        }

        diagnostics = await GetAdditionalAnalyzerDiagnosticsAsync(document, span, diagnosticIds, analyzerTypeName, cancellationToken).ConfigureAwait(false);
        if (!diagnostics.IsDefaultOrEmpty || string.IsNullOrWhiteSpace(syntheticDiagnosticId))
        {
            return diagnostics;
        }

        var syntheticDiagnostic = await CreateSyntheticDiagnosticAsync(document, span, syntheticDiagnosticId, cancellationToken).ConfigureAwait(false);
        return syntheticDiagnostic is null ? [] : [syntheticDiagnostic];
    }

    public async Task<ImmutableArray<Diagnostic>> GetProjectDiagnosticsAsync(
        Project project,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
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
            .Where(static diagnostic => !diagnostic.Location.IsInSource)
            .Where(diagnostic => diagnosticIds is null || diagnosticIds.Count == 0 || diagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToImmutableArray();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAdditionalAnalyzerDiagnosticsAsync(
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

        var analyzer = CreateDiagnosticAnalyzer(analyzerTypeName);
        if (analyzer is null)
        {
            return [];
        }

        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return [];
        }

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        if (syntaxTree is null)
        {
            return [];
        }

        var diagnostics = await compilation
            .WithAnalyzers([analyzer], document.Project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken)
            .ConfigureAwait(false);

        return diagnostics
            .Where(diagnostic => diagnostic.Location.IsInSource && diagnostic.Location.SourceTree == syntaxTree)
            .Where(diagnostic => diagnosticIds.Count == 0 || diagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .Where(diagnostic => span is null || diagnostic.Location.SourceSpan.IntersectsWith(span.Value))
            .ToImmutableArray();
    }

    private static DiagnosticAnalyzer? CreateDiagnosticAnalyzer(string analyzerTypeName)
    {
        try
        {
            var analyzerType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(analyzerTypeName, throwOnError: false, ignoreCase: false))
                .FirstOrDefault(static candidate => candidate is not null);
            if (analyzerType is null || !typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzerType))
            {
                return null;
            }

            return Activator.CreateInstance(analyzerType, nonPublic: true) as DiagnosticAnalyzer;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<Diagnostic?> CreateSyntheticDiagnosticAsync(
        Document document,
        TextSpan span,
        string diagnosticId,
        CancellationToken cancellationToken)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
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
