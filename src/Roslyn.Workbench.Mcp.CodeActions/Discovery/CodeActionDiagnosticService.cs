using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionDiagnosticService : ICodeActionDiagnosticService
{
    private const int _warningLimit = 20;

    private readonly ICodeActionBuiltInAnalyzerIndex _builtInAnalyzerIndex;

    public CodeActionDiagnosticService(ICodeActionBuiltInAnalyzerIndex builtInAnalyzerIndex)
    {
        _builtInAnalyzerIndex = builtInAnalyzerIndex;
    }

    public async Task<CodeActionProjectDiagnosticCollection> CollectProjectDiagnosticsAsync(
        Project project,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            var diagnosticsBySyntaxTree = new Dictionary<SyntaxTree, IReadOnlyList<Diagnostic>>();

            return new CodeActionProjectDiagnosticCollection([], [], diagnosticsBySyntaxTree, []);
        }

        var collection = await GetCompilationDiagnosticsAsync(
            project,
            compilation,
            diagnosticIds,
            cancellationToken);

        return CodeActionProjectDiagnosticCollection.Create(project, collection);
    }

    public async Task<CodeActionDiagnosticCollection> CollectDocumentDiagnosticsAsync(
        Document document,
        TextSpan? span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var collection = await CollectProjectDiagnosticsAsync(
            document.Project,
            diagnosticIds,
            cancellationToken);

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        var documentDiagnostics = syntaxTree is null
            ? []
            : collection.GetDocumentDiagnostics(syntaxTree, span);

        return new CodeActionDiagnosticCollection(documentDiagnostics, collection.Warnings);
    }

    public async Task<IReadOnlyList<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        TextSpan span,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var collection = await CollectDocumentDiagnosticsAsync(
            document,
            span,
            diagnosticIds,
            cancellationToken);

        return collection.Diagnostics;
    }

    public async Task<IReadOnlyList<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var collection = await CollectDocumentDiagnosticsAsync(
            document,
            span: null,
            diagnosticIds,
            cancellationToken);

        return collection.Diagnostics;
    }

    public async Task<IReadOnlyList<Diagnostic>> GetProjectDiagnosticsAsync(
        Project project,
        IReadOnlyList<string>? diagnosticIds,
        CancellationToken cancellationToken)
    {
        var collection = await CollectProjectDiagnosticsAsync(
            project,
            diagnosticIds,
            cancellationToken);

        return collection.ProjectDiagnostics;
    }

    private async Task<CodeActionDiagnosticCollection> GetCompilationDiagnosticsAsync(
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

        var analyzers = ImmutableArray.CreateBuilder<DiagnosticAnalyzer>();
        var analyzerTypes = new HashSet<Type>();
        var warnings = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        foreach (var analyzerReference in project.AnalyzerReferences)
        {
            ImmutableArray<DiagnosticAnalyzer> projectAnalyzers;
            try
            {
                projectAnalyzers = analyzerReference.GetAnalyzers(project.Language);
            }
            catch (Exception exception) when (IsExpectedAnalyzerMetadataFailure(exception))
            {
                AddAnalyzerWarning(
                    warnings,
                    analyzerReference.GetType(),
                    exception,
                    "loading project analyzers");

                continue;
            }

            foreach (var analyzer in projectAnalyzers)
            {
                if (diagnosticIdSet is not null
                    && !SupportsAnyDiagnostic(analyzer, diagnosticIdSet, warnings))
                {
                    continue;
                }

                if (analyzerTypes.Add(analyzer.GetType()))
                {
                    analyzers.Add(analyzer);
                }
            }
        }

        if (diagnosticIdSet is not null)
        {
            var builtInAnalyzers = _builtInAnalyzerIndex.GetAnalyzers(diagnosticIdSet);
            foreach (var analyzer in builtInAnalyzers)
            {
                if (analyzerTypes.Add(analyzer.GetType()))
                {
                    analyzers.Add(analyzer);
                }
            }
        }

        if (analyzers.Count == 0)
        {
            var compilerDiagnostics = compilation.GetDiagnostics(cancellationToken);
            var compilerDiagnosticsByIdentity = new Dictionary<string, Diagnostic>(StringComparer.Ordinal);
            AddMatchingDiagnostics(compilerDiagnosticsByIdentity, project, compilerDiagnostics, diagnosticIdSet);

            return CreateCollection(
                compilerDiagnosticsByIdentity,
                warnings,
                includeBuiltInWarnings: diagnosticIdSet is not null);
        }

        var analysisOptions = new CompilationWithAnalyzersOptions(
            project.AnalyzerOptions,
            (exception, analyzer, _) =>
            {
                var analyzerType = analyzer.GetType();
                var analyzerTypeName = analyzerType.FullName ?? analyzerType.Name;
                warnings.TryAdd(
                    $"Analyzer '{analyzerTypeName}' failed during diagnostic collection ({exception.GetType().Name}).",
                    0);
            },
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false);

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            analyzers.ToImmutable(),
            analysisOptions);

        var allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync(cancellationToken);
        var allDiagnosticsByIdentity = new Dictionary<string, Diagnostic>(StringComparer.Ordinal);
        AddMatchingDiagnostics(allDiagnosticsByIdentity, project, allDiagnostics, diagnosticIdSet);

        return CreateCollection(
            allDiagnosticsByIdentity,
            warnings,
            includeBuiltInWarnings: diagnosticIdSet is not null);
    }

    private static void AddMatchingDiagnostics(
        IDictionary<string, Diagnostic> destination,
        Project project,
        ImmutableArray<Diagnostic> diagnostics,
        HashSet<string>? diagnosticIds)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnosticIds is null || diagnosticIds.Contains(diagnostic.Id))
            {
                destination.TryAdd(CreateDiagnosticIdentity(project, diagnostic), diagnostic);
            }
        }
    }

    private CodeActionDiagnosticCollection CreateCollection(
        IReadOnlyDictionary<string, Diagnostic> diagnosticsByIdentity,
        ConcurrentDictionary<string, byte> executionWarnings,
        bool includeBuiltInWarnings)
    {
        var warnings = new List<string>(_warningLimit);
        if (includeBuiltInWarnings)
        {
            foreach (var warning in _builtInAnalyzerIndex.Warnings)
            {
                if (warnings.Count == _warningLimit)
                {
                    break;
                }

                warnings.Add(
                    $"Built-in analyzer '{warning.AnalyzerTypeName}' was unavailable ({warning.Status}).");
            }
        }

        foreach (var warning in executionWarnings.Keys.Order(StringComparer.Ordinal))
        {
            if (warnings.Count == _warningLimit)
            {
                break;
            }

            warnings.Add(warning);
        }

        return new CodeActionDiagnosticCollection(
            diagnosticsByIdentity.Values.ToArray(),
            warnings);
    }

    private static string CreateDiagnosticIdentity(Project project, Diagnostic diagnostic)
    {
        var builder = new StringBuilder();
        AppendIdentityPart(builder, project.Id.Id.ToString("N"));

        Document? document = null;
        if (diagnostic.Location.SourceTree is not null)
        {
            document = project.GetDocument(diagnostic.Location.SourceTree);
        }

        string? documentId = null;
        if (document is not null)
        {
            documentId = document.Id.Id.ToString("N");
        }

        AppendIdentityPart(builder, documentId);
        AppendIdentityPart(builder, diagnostic.Id);
        builder.Append(diagnostic.Location.IsInSource ? diagnostic.Location.SourceSpan.Start : -1);
        builder.Append(':');
        builder.Append(diagnostic.Location.IsInSource ? diagnostic.Location.SourceSpan.Length : -1);
        builder.Append('|');

        foreach (var property in diagnostic.Properties.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            AppendIdentityPart(builder, property.Key);
            AppendIdentityPart(builder, property.Value);
        }

        return builder.ToString();
    }

    private static void AppendIdentityPart(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:|");
            return;
        }

        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }

    private static bool SupportsAnyDiagnostic(
        DiagnosticAnalyzer analyzer,
        HashSet<string> diagnosticIds,
        ConcurrentDictionary<string, byte> warnings)
    {
        ImmutableArray<DiagnosticDescriptor> supportedDiagnostics;
        try
        {
            supportedDiagnostics = analyzer.SupportedDiagnostics;
        }
        catch (Exception exception) when (IsExpectedAnalyzerMetadataFailure(exception))
        {
            AddAnalyzerWarning(
                warnings,
                analyzer.GetType(),
                exception,
                "reading supported diagnostics");

            return false;
        }

        foreach (var descriptor in supportedDiagnostics)
        {
            if (diagnosticIds.Contains(descriptor.Id))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddAnalyzerWarning(
        ConcurrentDictionary<string, byte> warnings,
        Type componentType,
        Exception exception,
        string operation)
    {
        var componentTypeName = componentType.FullName ?? componentType.Name;
        warnings.TryAdd(
            $"Analyzer component '{componentTypeName}' failed while {operation} ({exception.GetType().Name}).",
            0);
    }

    private static bool IsExpectedAnalyzerMetadataFailure(Exception exception)
    {
        return exception is ArgumentException
            or BadImageFormatException
            or FileLoadException
            or FileNotFoundException
            or InvalidOperationException
            or NotSupportedException
            or ReflectionTypeLoadException
            or TypeLoadException;
    }
}
