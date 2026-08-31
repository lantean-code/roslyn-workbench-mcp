namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

/// <summary>
/// Returns language and optional detailed parse and analyzer-config options for a document.
/// </summary>
[RoslynTool("get-document-options", "Get Document Options", "Returns language and optional detailed parse and analyzer-config options for a document.")]
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
