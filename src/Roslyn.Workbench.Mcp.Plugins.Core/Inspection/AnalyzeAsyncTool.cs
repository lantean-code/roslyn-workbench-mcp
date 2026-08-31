namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Reports compiler and bundled analyzer findings for unsafe or inefficient asynchronous code.
/// </summary>
[RoslynTool("analyze-async", "Analyze Async", "Returns bundled AsyncFixer diagnostics and compiler diagnostic CS4014 for a selected scope.")]
internal sealed class AnalyzeAsyncTool : QueryToolHandler<AnalyzeAsyncRequest, AsyncAnalysisData>
{
    private readonly IAsyncAnalyzerDiagnosticService _asyncAnalyzerDiagnosticService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyzeAsyncTool"/> class.
    /// </summary>
    /// <param name="asyncAnalyzerDiagnosticService">The service that runs the bundled async analyzers.</param>
    public AnalyzeAsyncTool(IAsyncAnalyzerDiagnosticService asyncAnalyzerDiagnosticService)
    {
        _asyncAnalyzerDiagnosticService = asyncAnalyzerDiagnosticService;
    }

    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<AsyncAnalysisData>> ExecuteCoreAsync(
        AnalyzeAsyncRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        var documents = context.ToolExecutionServices.RequestResolver.ResolveDocuments<AsyncAnalysisData>(
            request.Scope,
            context);

        if (documents.HasRejection)
        {
            return documents.Rejection;
        }

        var compilerDiagnostics = await context.ToolExecutionServices.CompilerDiagnosticService
            .GetCompilerDiagnosticsAsync(documents.Value, cancellationToken);

        var analyzerDiagnostics = await _asyncAnalyzerDiagnosticService.GetAsyncAnalyzerDiagnosticsAsync(
            documents.Value,
            cancellationToken);

        var asyncDiagnostics = compilerDiagnostics
            .Where(static diagnostic => diagnostic.Id == "CS4014")
            .Concat(analyzerDiagnostics);

        var applicableDiagnostics = asyncDiagnostics
            .OrderBy(static diagnostic => diagnostic.Location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();

        var maxResults = request.EffectiveFindingsLimit;
        var findings = new List<AsyncFinding>();
        foreach (var diagnostic in applicableDiagnostics)
        {
            if (findings.Count == maxResults)
            {
                break;
            }

            findings.Add(new AsyncFinding
            {
                Diagnostic = CompilerDiagnosticHelpers.CreateDiagnosticInfo(diagnostic, context),
            });
        }

        var data = new AsyncAnalysisData
        {
            Findings = BoundedCollection.CreatePrebounded(findings, applicableDiagnostics.Length),
        };

        return PluginExecutionResult.Success(data);
    }
}
