using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionDiagnosticService : ICodeActionDiagnosticService
{
    private readonly ICodeActionAnalyzerActivator _analyzerActivator;

    public CodeActionDiagnosticService(ICodeActionAnalyzerActivator analyzerActivator)
    {
        _analyzerActivator = analyzerActivator;
    }

    public async Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var diagnostics = await GetDocumentDiagnosticsAsync(document, diagnosticIds, cancellationToken).ConfigureAwait(false);
        return diagnostics
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

        var diagnostics = await GetCompilationDiagnosticsAsync(
            document.Project,
            compilation,
            cancellationToken).ConfigureAwait(false);
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

        var diagnostics = await GetCompilationDiagnosticsAsync(project, compilation, cancellationToken).ConfigureAwait(false);
        return diagnostics
            .Where(static diagnostic => !diagnostic.Location.IsInSource)
            .Where(diagnostic => diagnosticIds is null || diagnosticIds.Count == 0 || diagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToImmutableArray();
    }

    private static async Task<ImmutableArray<Diagnostic>> GetCompilationDiagnosticsAsync(
        Project project,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var diagnostics = compilation.GetDiagnostics(cancellationToken).ToBuilder();
        var analyzers = project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(project.Language))
            .ToImmutableArray();
        if (analyzers.IsDefaultOrEmpty)
        {
            return diagnostics.ToImmutable();
        }

        var analyzerDiagnostics = await compilation
            .WithAnalyzers(analyzers, project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken)
            .ConfigureAwait(false);
        diagnostics.AddRange(analyzerDiagnostics);
        return diagnostics.ToImmutable();
    }

    private async Task<ImmutableArray<Diagnostic>> GetAdditionalAnalyzerDiagnosticsAsync(
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
            .WithAnalyzers([activation.Analyzer], document.Project.AnalyzerOptions)
            .GetAnalyzerDiagnosticsAsync(cancellationToken)
            .ConfigureAwait(false);

        return diagnostics
            .Where(diagnostic => diagnostic.Location.IsInSource && diagnostic.Location.SourceTree == syntaxTree)
            .Where(diagnostic => diagnosticIds.Count == 0 || diagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .Where(diagnostic => span is null || diagnostic.Location.SourceSpan.IntersectsWith(span.Value))
            .ToImmutableArray();
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
