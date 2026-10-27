namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns language and optional detailed parse and analyzer-config options for a document.
/// </summary>
[RoslynTool("get-document-options", "Get Document Options", "Inspects configuration for one selected document, returning language version and project-level nullable context. Use this to understand configuration behind diagnostic results; use get-diagnostics for findings or get-project-details for broader project configuration. Source nullable directives can override the reported project-level context. Detailed parse options and analyser configuration are omitted by default: request includeParseOptions for documentation mode and preprocessor symbols, or includeAnalyzerConfig for project configuration-file paths and syntax-tree analyser options.")]
internal sealed class GetDocumentOptionsTool : QueryToolHandler<GetDocumentOptionsRequest, DocumentOptionsData>
{
    /// <inheritdoc/>
    protected override async ValueTask<PluginExecutionResult<DocumentOptionsData>> ExecuteCoreAsync(GetDocumentOptionsRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var documentResolution = context.ToolExecutionServices.RequestResolver.ResolveDocument<DocumentOptionsData>(request.Document, context);
        if (documentResolution.HasRejection)
        {
            return documentResolution.Rejection;
        }

        var document = documentResolution.Value;
        var parseOptions = document.Project.ParseOptions;
        string? languageVersion = null;
        if (parseOptions is CSharpParseOptions csharpParseOptions)
        {
            languageVersion = csharpParseOptions.LanguageVersion.ToDisplayString();
        }

        string? nullableContext = null;
        if (document.Project.CompilationOptions is CSharpCompilationOptions csharpCompilationOptions)
        {
            nullableContext = csharpCompilationOptions.NullableContextOptions.ToString();
        }

        AnalyzerConfigInfo? analyzerConfig = null;
        if (request.IncludeAnalyzerConfig)
        {
            analyzerConfig = await InspectionProjectionFactory.CreateAnalyzerConfigInfoAsync(document, cancellationToken);
        }

        var data = new DocumentOptionsData
        {
            Document = context.WorkspaceResolver.CreateDocumentReference(document),
            LanguageVersion = languageVersion,
            NullableContext = nullableContext,
            ParseOptions = request.IncludeParseOptions
                ? InspectionProjectionFactory.CreateParseOptionsInfo(parseOptions)
                : null,
            AnalyzerConfig = analyzerConfig,
        };

        return PluginExecutionResult.Success(data);
    }
}
